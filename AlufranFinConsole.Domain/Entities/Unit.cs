namespace AlufranFinConsole.Domain.Entities;

public class Unit
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int Company_Id { get; set; }
    public string Type { get; set; } // operational, administrative
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Company Company { get; set; }
}
