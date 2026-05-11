namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Representa uma execução do pipeline de saneamento sobre uma FileVersion.
/// Um mesmo arquivo pode ter múltiplos runs (re-processamento).
/// </summary>
public class ProcessingRun
{
    public int Id { get; set; }
    public int FileVersion_Id { get; set; }
    public FileVersion FileVersion { get; set; } = null!;

    public string BaseType { get; set; } = "";      // PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG
    public string Competence { get; set; } = "";    // YYYY-MM

    public string Status { get; set; } = "PENDING"; // PENDING | RUNNING | COMPLETED | FAILED

    // Contadores do run
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DiscardedRows { get; set; }
    public int QaRows { get; set; }
    public int FactsGenerated { get; set; }

    // Resumo textual (JSON) com detalhes do processamento
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }

    public string StartedBy_Id { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
