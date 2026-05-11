namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Linha individual de staging no contexto de um ProcessingRun.
/// Contém o estado completo do pipeline para cada linha do arquivo.
/// </summary>
public class StagingRow
{
    public int Id { get; set; }
    public int ProcessingRun_Id { get; set; }
    public ProcessingRun ProcessingRun { get; set; } = null!;

    // Campos técnicos obrigatórios (spec §5)
    public string BaseType { get; set; } = "";
    public string Competence { get; set; } = "";
    public int ImportFileId { get; set; }
    public int FileVersionId { get; set; }
    public int OriginalRowNumber { get; set; }

    public string RawJson { get; set; } = "{}";
    public string NormalizedJson { get; set; } = "{}";
    public string LineHash { get; set; } = "";

    // VALID | DISCARDED | QA
    public string LineStatus { get; set; } = "VALID";
    public string? StatusReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
