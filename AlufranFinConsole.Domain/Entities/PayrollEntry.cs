namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Tabela definitiva de registros de folha de pagamento (FOPAG).
/// Populada na Fase 5 a partir dos registros StagingData do tipo FOPAG.
/// </summary>
public class PayrollEntry
{
    public int Id { get; set; }

    // Rastreabilidade
    public int ImportFile_Id { get; set; }
    public ImportFile ImportFile { get; set; } = null!;

    public int StagingData_Id { get; set; }
    public StagingData StagingData { get; set; } = null!;

    // Período
    public string Competence { get; set; } = "";   // YYYY-MM

    // Dados do funcionário
    public string Matricula { get; set; } = "";
    public string Funcionario { get; set; } = "";
    public string? FuncionarioKey { get; set; }    // chave normalizada
    public string? Cargo { get; set; }

    // Valores
    public decimal ValorBruto { get; set; }
    public decimal Descontos { get; set; }
    public decimal ValorLiquido { get; set; }

    // Controle
    public string Status { get; set; } = "ATIVO";  // ATIVO | CANCELADO
    public string? ProcessedBy_Id { get; set; }    // FK IdentityUser
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
