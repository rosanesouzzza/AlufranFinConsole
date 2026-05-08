namespace AlufranFinConsole.Domain.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string CNPJ { get; set; }
    public string SupplierKey { get; set; } // normalized key
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
