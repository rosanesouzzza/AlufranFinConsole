namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Fato financeiro validado, classificado e publicado.
/// Gerado SOMENTE a partir de StagingRow com LineStatus = "VALID" e ClassificationStatus completo.
/// Valores financeiros são somente-leitura após criação.
/// </summary>
public class FinancialFact
{
    public int Id { get; set; }
    public int ProcessingRun_Id { get; set; }
    public ProcessingRun ProcessingRun { get; set; } = null!;
    public int SourceStagingRow_Id { get; set; }

    public string BaseType { get; set; } = "";
    public string Competence { get; set; } = "";

    // Chaves de entidades mestras
    public int? Company_Id { get; set; }
    public int? Unit_Id { get; set; }
    public int? Supplier_Id { get; set; }
    public int? Client_Id { get; set; }
    public int? Service_Id { get; set; }
    public int? Product_Id { get; set; }
    public int? ChartOfAccount_Id { get; set; }
    public int? ErpCategory_Id { get; set; }

    public string? DocumentNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Valor pelo regime de competência</summary>
    public decimal AmountCompetence { get; set; }

    /// <summary>Valor pelo regime de caixa</summary>
    public decimal AmountCash { get; set; }

    // Classificação DRE — sempre vem do cadastro, nunca da linha
    public string? DreGroup { get; set; }
    public string? DreSubgroup { get; set; }
    public int? DreOrder { get; set; }

    /// <summary>Classified | UnclassifiedQa | PendingReview</summary>
    public string ClassificationStatus { get; set; } = "Classified";

    public DateTime CreatedAt { get; set; }
}
