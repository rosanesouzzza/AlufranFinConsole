namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Temporary staging table for data validation and sanitization before processing
/// Workflow: RawData → ParsedData (validate) → SanitizedData (sanitize) → Process
/// </summary>
public class StagingData
{
    public int Id { get; set; }

    // Reference to source file
    public int ImportFile_Id { get; set; }
    public ImportFile ImportFile { get; set; }

    // Line tracking
    public int LineNumber { get; set; }

    // Raw input
    public string RawData { get; set; } // Original CSV/TXT line

    // After parsing (CSV → fields)
    public string ParsedData { get; set; } // JSON: { "field1": value, "field2": value, ... }

    // Validation
    public string ValidationStatus { get; set; } // PENDING, VALID, INVALID, DUPLICATE, PROCESSED
    public string ValidationErrors { get; set; } // JSON: [{ "field": "supplier", "error": "Not found" }]

    // After sanitization (normalize values, apply rules)
    public string SanitizedData { get; set; } // JSON: { "supplier_id": 123, "normalized_name": "..." }

    // Processing timestamp
    public DateTime? ProcessedAt { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
