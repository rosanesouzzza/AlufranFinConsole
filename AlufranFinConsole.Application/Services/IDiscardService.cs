using System.Text.Json;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Avalia se uma linha normalizada deve ser descartada.
/// Regras globais — spec §9. Regras por base chamadas pelo FinancialSanitizationService.
/// </summary>
public interface IDiscardService
{
    /// <summary>
    /// Verifica regras globais de descarte aplicáveis a todos os tipos de base.
    /// Retorna o motivo de descarte ou null se a linha deve ser mantida.
    /// </summary>
    DiscardCheck Check(IReadOnlyDictionary<string, string> normalizedRow, string baseType);
}

public sealed record DiscardCheck(bool ShouldDiscard, string? Reason, string? Detail = null);

public class DiscardService : IDiscardService
{
    private static readonly string[] TotalKeywords =
        ["TOTAL GERAL", "TOTAL:", "GRAND TOTAL", "TOTAIS", "SUBTOTAL", "SUB-TOTAL",
         "TOTAL POR", "SOMA TOTAL", "TOTAL DO PERÍODO"];

    private static readonly string[] HeaderKeywords =
        ["FORNECEDOR", "CLIENTE", "DOCUMENTO", "NÚMERO", "VALOR", "DATA"];

    private static readonly string[] SeparatorPatterns =
        ["----", "====", "####", "...."];

    /// <summary>
    /// Colunas obrigatórias mínimas por base. Se NENHUMA delas estiver presente,
    /// a linha tem estrutura inválida (InvalidStructure).
    /// </summary>
    private static readonly Dictionary<string, string[]> RequiredStructureFields = new()
    {
        ["PAG"]     = ["SupplierName","TitleAmount"],
        ["REC"]     = ["ClientName","TitleAmount"],
        ["FAT"]     = ["ClientName","TotalAmount"],
        ["EMITIDAS"]= ["InvoiceNumber","GrossAmount"],
        ["COMP"]    = ["SupplierName","TotalAmount"],
        ["TRANSF"]  = ["SourceAccount","DestinationAccount"],
        ["FOPAG"]   = ["PayrollItemName","PayrollAmount"],
    };

    /// <summary>
    /// Campos financeiros por base para regra MissingFinancialValue.
    /// NOTA: regra refinada por base — não descarta linhas com cancelamento,
    /// estorno ou compensação explícitos; elas seguem para QA.
    /// </summary>
    private static readonly Dictionary<string, (string[] Fields, string[] ExemptStatusValues)> FinancialConfig = new()
    {
        // PAG: exige TitleAmount OU PaidAmount > 0, mas exclui títulos cancelados/compensados
        ["PAG"] = (["TitleAmount","PaidAmount"],
                   ["CANCELADO","CANCELADA","COMPENSADO","COMPENSADA","ESTORNADO"]),
        // REC: exige TitleAmount > 0; títulos cancelados/compensados são exempts
        ["REC"] = (["TitleAmount","ReceivedAmount"],
                   ["CANCELADO","CANCELADA","COMPENSADO","COMPENSADA"]),
        // FAT: exige TotalAmount > 0; serviços com desconto integral têm status DESCONTO/ISENTO
        ["FAT"] = (["TotalAmount"],
                   ["DESCONTO INTEGRAL","ISENTO","CANCELADO"]),
        // EMITIDAS: GrossAmount pode ser 0 em notas canceladas — status é o guardião
        ["EMITIDAS"] = (["GrossAmount","NetAmount"],
                        ["CANCELADA","CANCELLED","CANCELADO"]),
        // COMP: TotalAmount > 0 ou ajuste zero formal
        ["COMP"] = (["TotalAmount"],
                    ["AJUSTE","CANCELADO"]),
        // TRANSF: TransferAmount pode ser 0 em ajustes de zeramento de conta
        ["TRANSF"] = (["TransferAmount"],
                      ["AJUSTE ZERAMENTO","ZERAMENTO"]),
        // FOPAG: PayrollAmount pode ser 0 em rubricas de desconto integral
        ["FOPAG"] = (["PayrollAmount"],
                     ["DESCONTO INTEGRAL","COMPENSACAO"]),
    };

    public DiscardCheck Check(IReadOnlyDictionary<string, string> row, string baseType)
    {
        // 1. Linha totalmente vazia
        if (row.Count == 0 || row.Values.All(string.IsNullOrWhiteSpace))
            return new(true, "EmptyRow");

        var allValues = string.Join(" ", row.Values).Trim().ToUpperInvariant();

        // 2. Cabeçalho repetido (≥3 labels de cabeçalho presentes)
        var headerHits = HeaderKeywords.Count(k => allValues.Contains(k));
        if (headerHits >= 3)
            return new(true, "RepeatedHeader", $"hits={headerHits}");

        // 3 & 4. Total geral / subtotal
        foreach (var kw in TotalKeywords)
            if (allValues.Contains(kw))
                return new(true, allValues.Contains("SUBTOTAL") || allValues.Contains("SUB-TOTAL")
                    ? "Subtotal" : "GrandTotal", kw);

        // 5. Separador visual
        foreach (var sep in SeparatorPatterns)
            if (allValues.Contains(sep))
                return new(true, "VisualSeparator");

        var bt = baseType.ToUpperInvariant();

        // 6. InvalidStructure — nenhuma coluna estrutural mínima presente para a base
        if (RequiredStructureFields.TryGetValue(bt, out var reqFields))
        {
            bool hasAnyStructural = reqFields.Any(f =>
                row.TryGetValue(f, out var v) && !string.IsNullOrWhiteSpace(v));
            if (!hasAnyStructural)
                return new(true, "InvalidStructure",
                    $"Nenhum campo estrutural encontrado para baseType={baseType}: {string.Join(",", reqFields)}");
        }

        // 7. MissingFinancialValue — refinado por base, com isenção de status
        if (FinancialConfig.TryGetValue(bt, out var finCfg))
        {
            bool hasFinancialValue = finCfg.Fields.Any(f =>
                row.TryGetValue(f, out var v) &&
                !string.IsNullOrWhiteSpace(v) &&
                decimal.TryParse(v, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) &&
                d != 0m);

            if (!hasFinancialValue)
            {
                // Verificar se linha tem status isento — se sim, não descarta (vai para QA)
                var statusValue = (
                    row.GetValueOrDefault("TitleStatus") ??
                    row.GetValueOrDefault("InvoiceStatus") ??
                    row.GetValueOrDefault("TransferType") ??
                    row.GetValueOrDefault("PayrollStatus") ?? "").ToUpperInvariant();

                bool isExempt = finCfg.ExemptStatusValues.Any(s => statusValue.Contains(s));
                if (!isExempt)
                    return new(true, "MissingFinancialValue",
                        $"baseType={baseType}, fields checked: {string.Join(",", finCfg.Fields)}");
                // Isento → não descarta; a linha segue para QA (ex: NF cancelada com GrossAmount=0)
            }
        }

        // 8. TechnicalDuplicate — detectado pelo hash na camada de pipeline (FinancialSanitizationService)
        //    O DiscardService não tem acesso ao banco; duplicatas são sinalizadas externamente.
        //    Registrado aqui como ponto formal de extensão — impl. no pipeline.

        // 9. OutOfCompetence — verificado pelo pipeline com base no campo Competence vs competência do run
        //    Registrado aqui como ponto formal de extensão — impl. no pipeline.

        return new(false, null);
    }
}
