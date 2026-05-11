namespace AlufranFinConsole.Domain.Entities;

public class ErpCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ErpCategoryKey { get; set; } = "";
    public string? Description { get; set; }

    // Classificação DRE — vem do cadastro de categorias, nunca da linha financeira
    public string? DreGroup    { get; set; }
    public string? DreSubgroup { get; set; }
    public int?    DreOrder    { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
