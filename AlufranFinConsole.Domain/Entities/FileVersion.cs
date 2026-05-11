namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Versão imutável de um arquivo importado.
/// Cada re-upload gera uma nova versão sem sobrescrever a anterior.
/// </summary>
public class FileVersion
{
    public int Id { get; set; }
    public int ImportFile_Id { get; set; }
    public ImportFile ImportFile { get; set; } = null!;

    public int VersionNumber { get; set; }           // 1, 2, 3...
    public string FileHash { get; set; } = "";       // SHA-256 do conteúdo
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";  // ACTIVE | SUPERSEDED | ARCHIVED
    public string? Notes { get; set; }               // motivo da re-importação

    public string CreatedBy_Id { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
