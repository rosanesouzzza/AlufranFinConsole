namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Problema de qualidade detectado durante o pipeline de saneamento.
/// Severidade Blocking impede aprovação do fechamento.
/// </summary>
public class QaIssue
{
    public int Id { get; set; }
    public int ProcessingRun_Id { get; set; }
    public ProcessingRun ProcessingRun { get; set; } = null!;
    public int FileVersion_Id { get; set; }

    public string BaseType { get; set; } = "";
    public string Competence { get; set; } = "";
    public int OriginalRowNumber { get; set; }

    /// <summary>
    /// Tipo — spec §10:
    /// UnclassifiedCategory | SupplierNotMapped | ClientNotMapped | UnitNotMapped |
    /// CompanyNotMapped | ServiceNotMapped | ProductNotMapped | InvalidAmount |
    /// InvalidDate | MissingDocument | CompetenceMismatch | SuspiciousDuplicate |
    /// CancelledWithAmount | MissingRequiredField
    /// </summary>
    public string IssueType { get; set; } = "";

    /// <summary>Info | Warning | Blocking</summary>
    public string Severity { get; set; } = "Warning";

    public string Message { get; set; } = "";
    public string RawJson { get; set; } = "{}";
    public string NormalizedJson { get; set; } = "{}";

    /// <summary>Open | Resolved | Suppressed</summary>
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
