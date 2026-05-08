namespace AlufranFinConsole.Domain.Entities;

public class ChartOfAccount
{
    public int Id { get; set; }
    public string Number { get; set; }
    public string Name { get; set; }
    public string Type { get; set; } // revenue, expense, asset, liability
    public int Level { get; set; }
    public int? Parent_Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ChartOfAccount Parent { get; set; }
}
