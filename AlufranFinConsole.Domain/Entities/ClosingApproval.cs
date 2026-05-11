namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Registro de aprovação formal de um fechamento financeiro por competência.
/// Cada competência pode ter no máximo uma aprovação APROVADO vigente.
/// Reaberturas geram novo registro com status REABERTO.
/// </summary>
public class ClosingApproval
{
    public int      Id              { get; set; }
    public string   Competence      { get; set; } = "";    // "YYYY-MM"
    public string   Status          { get; set; } = "";    // APROVADO | REABERTO

    // Quem aprovou / reabriu
    public string   ApprovedBy      { get; set; } = "";    // e-mail do usuário
    public DateTime ApprovedAt      { get; set; }

    // Notas de aprovação / reabertura
    public string?  Notes           { get; set; }

    // Snapshot da DRE no momento da aprovação (JSON serializado)
    public string?  DreSnapshot     { get; set; }

    // Campos de rastreabilidade
    public DateTime CreatedAt       { get; set; }
    public DateTime? UpdatedAt      { get; set; }

    // -- Campos calculados (não persistidos) --
    public bool IsAprovado => Status == "APROVADO";
}
