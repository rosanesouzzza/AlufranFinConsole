namespace AlufranFinConsole.Domain.Entities;

public class ClassificationRule
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Priority { get; set; }
    public string RuleType { get; set; } // FixedSupplier, ERP, ChartOfAccount, Product
    public string Condition { get; set; } // JSON
    public string Result { get; set; } // JSON
    public bool IsActive { get; set; } = true;
    public int CreatedBy_Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual User CreatedBy { get; set; }
}
