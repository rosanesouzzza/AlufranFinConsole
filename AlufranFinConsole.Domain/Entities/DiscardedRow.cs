namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Linha descartada com rastreabilidade completa e motivo auditável.
/// Jamais deletar — descartes são persistidos para auditoria.
/// </summary>
public class DiscardedRow
{
    public int Id { get; set; }
    public int ProcessingRun_Id { get; set; }
    public ProcessingRun ProcessingRun { get; set; } = null!;
    public int FileVersion_Id { get; set; }

    public string BaseType { get; set; } = "";
    public string Competence { get; set; } = "";
    public int OriginalRowNumber { get; set; }

    public string RawJson { get; set; } = "{}";

    /// <summary>
    /// Motivo padronizado — spec §9:
    /// EmptyRow | RepeatedHeader | GrandTotal | Subtotal | VisualSeparator |
    /// MissingFinancialValue | InvalidStructure | OutOfCompetence | TechnicalDuplicate
    /// </summary>
    public string DiscardReason { get; set; } = "";
    public string? DiscardDetail { get; set; }   // informação adicional livre

    public DateTime CreatedAt { get; set; }
}
