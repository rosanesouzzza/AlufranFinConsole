namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Avalia regras de QA para cada tipo de base — spec §10.
/// </summary>
public interface IQaIssueService
{
    /// <summary>
    /// Executa todas as regras de QA aplicáveis ao row normalizado e retorna
    /// a lista de problemas encontrados (pode ser vazia).
    /// </summary>
    IReadOnlyList<QaCheck> Check(
        IReadOnlyDictionary<string, string> normalizedRow,
        string baseType,
        string competence);
}

public sealed record QaCheck(string IssueType, string Severity, string Message);

public class QaIssueService : IQaIssueService
{
    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    public IReadOnlyList<QaCheck> Check(
        IReadOnlyDictionary<string, string> row,
        string baseType,
        string competence)
    {
        var issues = new List<QaCheck>();

        issues.AddRange(baseType.ToUpperInvariant() switch
        {
            "PAG"     => CheckPag(row, competence),
            "REC"     => CheckRec(row, competence),
            "FAT"     => CheckFat(row, competence),
            "EMITIDAS"=> CheckEmitidas(row, competence),
            "COMP"    => CheckComp(row, competence),
            "TRANSF"  => CheckTransf(row, competence),
            "FOPAG"   => CheckFopag(row, competence),
            _ => []
        });

        return issues;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool Empty(IReadOnlyDictionary<string, string> row, string field) =>
        !row.TryGetValue(field, out var v) || string.IsNullOrWhiteSpace(v);

    private static bool HasValue(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var v) && !string.IsNullOrWhiteSpace(v);

    private static decimal GetDecimal(IReadOnlyDictionary<string, string> row, string field)
    {
        if (row.TryGetValue(field, out var v) &&
            decimal.TryParse(v, System.Globalization.NumberStyles.Any, Inv, out var d)) return d;
        return 0m;
    }

    private static bool ValidDate(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var v) &&
        DateTime.TryParse(v, out _);

    private static QaCheck Warning(string type, string msg) => new(type, "Warning", msg);
    private static QaCheck Blocking(string type, string msg) => new(type, "Blocking", msg);
    private static QaCheck Info(string type, string msg)    => new(type, "Info", msg);

    // ── PAG ──────────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckPag(IReadOnlyDictionary<string, string> r, string comp)
    {
        var titleAmt = GetDecimal(r, "TitleAmount");
        var paidAmt  = GetDecimal(r, "PaidAmount");
        var status   = r.GetValueOrDefault("TitleStatus", "").ToUpperInvariant();
        bool isPaid  = status is "PAGO" or "LIQUIDADO" or "PAID";

        // Blocking: dados mestres ausentes
        if (Empty(r,"SupplierName") && titleAmt != 0)
            yield return Blocking("SupplierNotMapped","Fornecedor vazio com valor financeiro");
        if (Empty(r,"ErpCategoryName"))
            yield return Blocking("UnclassifiedCategory","Categoria ERP vazia");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — DRE e consolidação comprometidos");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada — centro de custo comprometido");
        // Blocking: datas inválidas
        if (HasValue(r,"DueDate") && !ValidDate(r,"DueDate"))
            yield return Blocking("InvalidDate","DueDate inválida");
        // Blocking: valor estruturalmente inválido
        if (isPaid && paidAmt == 0)
            yield return Blocking("InvalidAmount","Título pago sem PaidAmount — dados estruturalmente inconsistentes");
        // Warning: alertas operacionais
        if (Empty(r,"DocumentNumber") && titleAmt != 0)
            yield return Warning("MissingDocument","Documento vazio com valor");
        if (paidAmt > titleAmt && titleAmt > 0)
            yield return Warning("InvalidAmount",$"PaidAmount ({paidAmt}) > TitleAmount ({titleAmt})");
        if (titleAmt < 0)
            yield return Warning("InvalidAmount","Valor título negativo — verificar estorno");
        if (isPaid && Empty(r,"PaymentDate"))
            yield return Warning("MissingDocument","PaymentDate vazia para título pago");
        if (HasValue(r,"CompetenceDate") && !HasValue(r,"Competence"))
            yield return Info("CompetenceMismatch","Data de competência presente mas campo Competence vazio");
    }

    // ── REC ──────────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckRec(IReadOnlyDictionary<string, string> r, string comp)
    {
        var titleAmt = GetDecimal(r,"TitleAmount");
        var recAmt   = GetDecimal(r,"ReceivedAmount");
        var status   = r.GetValueOrDefault("TitleStatus","").ToUpperInvariant();
        bool isRec   = status is "RECEBIDO" or "LIQUIDADO" or "RECEIVED";

        // Blocking: dados mestres ausentes
        if (Empty(r,"ClientName") && titleAmt != 0)
            yield return Blocking("ClientNotMapped","Cliente vazio com valor financeiro");
        if (Empty(r,"ErpCategoryName"))
            yield return Blocking("UnclassifiedCategory","Categoria ERP vazia");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — consolidação comprometida");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada — centro de custo comprometido");
        // Blocking: valor estruturalmente inválido
        if (isRec && recAmt == 0)
            yield return Blocking("InvalidAmount","Título recebido sem ReceivedAmount — inconsistência estrutural");
        // Warning
        if (Empty(r,"DocumentNumber") && titleAmt != 0)
            yield return Warning("MissingDocument","Documento vazio com valor");
        if (recAmt > titleAmt && titleAmt > 0)
            yield return Warning("InvalidAmount",$"ReceivedAmount ({recAmt}) > TitleAmount ({titleAmt})");
        if (titleAmt < 0)
            yield return Warning("InvalidAmount","Valor negativo sem regra formal");
        if (isRec && Empty(r,"ReceiptDate"))
            yield return Warning("MissingDocument","ReceiptDate vazia para título recebido");
    }

    // ── FAT ──────────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckFat(IReadOnlyDictionary<string, string> r, string comp)
    {
        var total  = GetDecimal(r,"TotalAmount");
        var unit   = GetDecimal(r,"UnitAmount");
        var qty    = GetDecimal(r,"Quantity");
        var calc   = unit * qty;

        // Blocking: dados mestres ausentes
        if (Empty(r,"ClientName") && total != 0)
            yield return Blocking("ClientNotMapped","Cliente vazio com valor");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — faturamento sem empresa");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada");
        // Blocking: divergência Qty×Unit estruturalmente inválida
        if (calc != 0 && Math.Abs(total - calc) > 0.05m)
            yield return Blocking("InvalidAmount",
                $"TotalAmount ({total:F2}) ≠ Qty×Unit ({calc:F2}) — tolerância de R$0,05 excedida");
        // Warning
        if (Empty(r,"ServiceName") && total != 0)
            yield return Warning("ServiceNotMapped","Serviço vazio com valor");
        if (qty < 0)
            yield return Warning("InvalidAmount","Quantidade negativa sem regra");
        if (unit < 0)
            yield return Warning("InvalidAmount","Valor unitário negativo sem regra");
        if (Empty(r,"BillingDocument") && total != 0)
            yield return Info("MissingDocument","Documento ausente em linha com valor");
    }

    // ── EMITIDAS ─────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckEmitidas(IReadOnlyDictionary<string, string> r, string comp)
    {
        var gross  = GetDecimal(r,"GrossAmount");
        var net    = GetDecimal(r,"NetAmount");
        var tax    = GetDecimal(r,"TaxAmount");
        var status = r.GetValueOrDefault("InvoiceStatus","").ToUpperInvariant();
        bool canc  = status is "CANCELADA" or "CANCELLED" or "CANCELADO";

        // Blocking
        if (Empty(r,"InvoiceNumber"))
            yield return Blocking("MissingDocument","Nota sem número");
        if (Empty(r,"ClientName") && gross != 0)
            yield return Blocking("ClientNotMapped","Cliente vazio com valor");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — NF sem emitente");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada");
        if (canc && gross != 0)
            yield return Blocking("CancelledWithAmount","Nota cancelada com valor diferente de zero");
        if (HasValue(r,"IssueDate") && !ValidDate(r,"IssueDate"))
            yield return Blocking("InvalidDate","IssueDate inválida");
        // Blocking: estrutura financeira inválida
        if (gross < net && net != 0)
            yield return Blocking("InvalidAmount","GrossAmount < NetAmount — NF estruturalmente inválida");
        // Warning
        if (tax < 0)
            yield return Warning("InvalidAmount","TaxAmount negativo sem regra");
    }

    // ── COMP ─────────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckComp(IReadOnlyDictionary<string, string> r, string comp)
    {
        var total = GetDecimal(r,"TotalAmount");
        var unit  = GetDecimal(r,"UnitAmount");
        var qty   = GetDecimal(r,"Quantity");
        var calc  = unit * qty;

        // Blocking: dados mestres ausentes
        if (Empty(r,"SupplierName") && total != 0)
            yield return Blocking("SupplierNotMapped","Fornecedor vazio com valor");
        if (Empty(r,"ErpCategoryName"))
            yield return Blocking("UnclassifiedCategory","Categoria ERP vazia");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — COMP sem empresa");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada");
        // Blocking: divergência Qty×Unit
        if (calc != 0 && Math.Abs(total - calc) > 0.05m)
            yield return Blocking("InvalidAmount",
                $"TotalAmount ({total:F2}) ≠ Qty×Unit ({calc:F2})");
        // Warning
        if (Empty(r,"ProductName") && total != 0)
            yield return Warning("ProductNotMapped","Produto vazio com valor");
        if (qty < 0)
            yield return Warning("InvalidAmount","Quantidade negativa sem regra");
        if (Empty(r,"PurchaseDocument") && total != 0)
            yield return Info("MissingDocument","Documento de compra ausente com valor");
    }

    // ── TRANSF ───────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckTransf(IReadOnlyDictionary<string, string> r, string comp)
    {
        var amount = GetDecimal(r,"TransferAmount");

        // Blocking: campos obrigatórios
        if (Empty(r,"SourceAccount"))
            yield return Blocking("MissingRequiredField","Conta origem vazia");
        if (Empty(r,"DestinationAccount"))
            yield return Blocking("MissingRequiredField","Conta destino vazia");
        var src = r.GetValueOrDefault("SourceAccount","").Trim().ToUpperInvariant();
        var dst = r.GetValueOrDefault("DestinationAccount","").Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(src) && src == dst)
            yield return Blocking("InvalidAmount","Origem e destino iguais — transferência sem efeito");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa origem não identificada");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade origem não identificada");
        if (HasValue(r,"TransferDate") && !ValidDate(r,"TransferDate"))
            yield return Blocking("InvalidDate","TransferDate inválida");
        // Warning
        if (amount < 0)
            yield return Warning("InvalidAmount","Valor negativo sem regra");
        // TRANSF não deve ser classificada como receita/despesa — Blocking
        var type = r.GetValueOrDefault("TransferType","").ToUpperInvariant();
        if (type is "RECEITA" or "DESPESA" or "REVENUE" or "EXPENSE")
            yield return Blocking("UnclassifiedCategory",
                "Tentativa de classificar TRANSF como receita/despesa sem regra formal");
    }

    // ── FOPAG ────────────────────────────────────────────────────────────────
    private static IEnumerable<QaCheck> CheckFopag(IReadOnlyDictionary<string, string> r, string comp)
    {
        var amount = GetDecimal(r,"PayrollAmount");

        // Blocking: campos obrigatórios
        if (Empty(r,"PayrollItemName") && amount != 0)
            yield return Blocking("UnclassifiedCategory","Rubrica vazia com valor");
        if (Empty(r,"CompanyName"))
            yield return Blocking("CompanyNotMapped","Empresa não identificada — folha sem empresa");
        if (Empty(r,"UnitName"))
            yield return Blocking("UnitNotMapped","Unidade não identificada");
        if (Empty(r,"CompetenceDate"))
            yield return Blocking("MissingRequiredField","Competência ausente na FOPAG");
        // Warning
        if (Empty(r,"CostCenterName"))
            yield return Warning("UnitNotMapped","Centro de custo não mapeado");
        if (amount < 0)
            yield return Warning("InvalidAmount","Valor negativo — verificar natureza da rubrica");
    }
}
