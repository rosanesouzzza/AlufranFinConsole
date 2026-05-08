namespace AlufranFinConsole.Domain.Entities;

public class Service
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string ServiceKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
