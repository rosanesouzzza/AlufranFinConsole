namespace AlufranFinConsole.Domain.Entities;

public class ColumnMapping
{
    public int Id { get; set; }
    public string FileType { get; set; } // PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG
    public string SourceColumn { get; set; }
    public string TargetColumn { get; set; }
    public string DataType { get; set; } // string, decimal, datetime, int, bool
    public string Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
