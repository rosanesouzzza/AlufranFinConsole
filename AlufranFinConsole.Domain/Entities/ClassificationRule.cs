namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Regra de classificação DRE aplicável a um ou mais tipos de base.
/// BaseType = "*" aplica a todos.
/// </summary>
public class ClassificationRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; }

    /// <summary>FixedSupplier | ErpCategory | ChartOfAccount | Product | Service | Payroll</summary>
    public string RuleType { get; set; } = "";

    /// <summary>PAG | REC | FAT | EMITIDAS | COMP | TRANSF | FOPAG | * (todos)</summary>
    public string BaseType { get; set; } = "*";

    /// <summary>Condição simples: "Field=Value" ou "Field~=PartialValue"</summary>
    public string Condition { get; set; } = "";

    /// <summary>JSON com dados de resultado (legado / debug)</summary>
    public string? Result { get; set; }

    // Resultado classificatório — vem sempre do cadastro
    public string? DreGroup    { get; set; }
    public string? DreSubgroup { get; set; }
    public int?    DreOrder    { get; set; }
    public int?    ChartOfAccount_Id { get; set; }
    public int?    ErpCategory_Id    { get; set; }

    public bool IsActive { get; set; } = true;
    public string? CreatedBy_Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
