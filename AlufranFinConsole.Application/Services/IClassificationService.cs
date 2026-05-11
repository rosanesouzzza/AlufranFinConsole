using AlufranFinConsole.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Aplica regras de classificação DRE a partir do cadastro.
/// Jamais classifica hardcoded por base — spec §8 e §22.
/// </summary>
public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(
        IReadOnlyDictionary<string, string> normalizedRow,
        string baseType,
        CancellationToken ct = default);
}

public sealed record ClassificationResult(
    bool Classified,
    string? DreGroup,
    string? DreSubgroup,
    int? DreOrder,
    int? ChartOfAccountId,
    int? ErpCategoryId,
    string? UnclassifiedReason);

public class ClassificationService : IClassificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ITextNormalizationService _norm;

    public ClassificationService(IApplicationDbContext context, ITextNormalizationService norm)
    {
        _context = context;
        _norm    = norm;
    }

    /// <summary>
    /// Precedência de classificação — spec §22:
    ///   Nível 1 (Priority 10-19): Fornecedor ou cliente específico   — RuleType "FixedSupplier" / "FixedClient"
    ///   Nível 2 (Priority 20-29): Categoria ERP do cadastro          — lookup ErpCategoryKey
    ///   Nível 3 (Priority 30-39): Produto, serviço ou rubrica        — RuleType "Product" / "Service" / "PayrollItem"
    ///   Nível 4 (Priority 40-49): Plano DRE / conta contábil         — RuleType "ChartOfAccount"
    ///   Nível 5 (Priority 50-99): Regra formal genérica              — RuleType "FixedSupplier"/"Generic"/…
    ///   Nível 6: não classificado → QA Blocking UnclassifiedCategory
    /// ClassificationRule.Priority determina a ordem; faixas são convencão, não constraint.
    /// </summary>
    public async Task<ClassificationResult> ClassifyAsync(
        IReadOnlyDictionary<string, string> row,
        string baseType,
        CancellationToken ct = default)
    {
        // Carrega todas as regras ativas para a base (inclui regras "*") ordenadas por prioridade
        var rules = await _context.ClassificationRules
            .Where(r => r.IsActive && (r.BaseType == baseType || r.BaseType == "*"))
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        // Nível 1: Fornecedor/Cliente específico (Priority 10-19 por convenção)
        var specificRule = rules
            .Where(r => r.RuleType is "FixedSupplier" or "FixedClient" or "FixedPayee")
            .FirstOrDefault(r => RuleMatches(r, row));
        if (specificRule != null)
            return BuildResult(specificRule);

        // Nível 2: Categoria ERP — lookup soberano no cadastro
        if (row.TryGetValue("ErpCategoryName", out var catRaw) && !string.IsNullOrWhiteSpace(catRaw))
        {
            var catKey = _norm.NormalizeKey(catRaw);
            var cat = await _context.ErpCategories
                .FirstOrDefaultAsync(c => c.ErpCategoryKey == catKey, ct);
            if (cat != null)
                return new(true, cat.DreGroup, cat.DreSubgroup, cat.DreOrder, null, cat.Id, null);
        }

        // Nível 3: Produto, serviço ou rubrica (Priority 30-39)
        var productRule = rules
            .Where(r => r.RuleType is "Product" or "Service" or "PayrollItem")
            .FirstOrDefault(r => RuleMatches(r, row));
        if (productRule != null)
            return BuildResult(productRule);

        // Nível 4: Plano DRE / conta contábil (Priority 40-49)
        var chartRule = rules
            .Where(r => r.RuleType is "ChartOfAccount" or "DRE")
            .FirstOrDefault(r => RuleMatches(r, row));
        if (chartRule != null)
            return BuildResult(chartRule);

        // Nível 5: Qualquer regra formal restante (Priority 50+) — varredura completa por ordem
        var genericRule = rules.FirstOrDefault(r => RuleMatches(r, row));
        if (genericRule != null)
            return BuildResult(genericRule);

        // Nível 6: Não classificado → QA
        return new(false, null, null, null, null, null,
            $"Sem regra de classificação para baseType={baseType}; " +
            $"ErpCategoryName={row.GetValueOrDefault("ErpCategoryName","(vazio)")}");
    }

    private static ClassificationResult BuildResult(ClassificationRule rule) =>
        new(true, rule.DreGroup, rule.DreSubgroup, rule.DreOrder,
            rule.ChartOfAccount_Id, rule.ErpCategory_Id, null);

    private static bool RuleMatches(ClassificationRule rule, IReadOnlyDictionary<string, string> row)
    {
        if (string.IsNullOrWhiteSpace(rule.Condition)) return false;
        try
        {
            // Suporta múltiplas condições separadas por ponto-e-vírgula (AND lógico)
            var conditions = rule.Condition.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return conditions.All(cond => EvalCondition(cond.Trim(), row));
        }
        catch { return false; }
    }

    private static bool EvalCondition(string condition, IReadOnlyDictionary<string, string> row)
    {
        // "Field~=PartialValue" (contém, case-insensitive)
        // "Field=Value"         (exato, case-insensitive)
        var parts = condition.Split("=", 2);
        if (parts.Length != 2) return false;
        var field  = parts[0].Replace("~", "").Trim();
        var value  = parts[1].Trim();
        var isLike = parts[0].TrimEnd().EndsWith("~");

        if (!row.TryGetValue(field, out var rowValue)) return false;
        return isLike
            ? rowValue.Contains(value, StringComparison.OrdinalIgnoreCase)
            : rowValue.Equals(value, StringComparison.OrdinalIgnoreCase);
    }
}
