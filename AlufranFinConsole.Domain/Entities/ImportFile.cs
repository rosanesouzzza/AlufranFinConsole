namespace AlufranFinConsole.Domain.Entities;

public class ImportFile
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public string FileHash { get; set; }
    public long FileSize { get; set; }
    public int UploadedBy_Id { get; set; }
    public DateTime UploadedAt { get; set; }
    public string FileType { get; set; } // PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG
    public string Competence { get; set; } // YYYY-MM
    public string Status { get; set; } // pending, processing, completed, failed
    public string StoragePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual User UploadedBy { get; set; }
}
