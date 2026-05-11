namespace AlufranFinConsole.Domain.Entities;

/// <summary>
/// Tabela definitiva de transações financeiras consolidadas.
/// Populada na Fase 5 a partir dos registros StagingData com status VALID/SANITIZED.
/// Cobre os tipos: PAG, REC, FAT, EMITIDAS, COMP, TRANSF.
/// </summary>
public class FinancialTransaction
{
    public int Id { get; set; }

    // Rastreabilidade
    public int ImportFile_Id { get; set; }
    public ImportFile ImportFile { get; set; } = null!;

    public int StagingData_Id { get; set; }
    public StagingData StagingData { get; set; } = null!;

    // Tipo e período
    public string TransactionType { get; set; } = "";   // PAG | REC | FAT | EMITIDAS | COMP | TRANSF
    public string Competence { get; set; } = "";         // YYYY-MM

    // Dados financeiros
    public string Documento { get; set; } = "";          // Número NF, duplicata, TRF, etc.
    public string? Counterpart { get; set; }             // Fornecedor/Cliente/Conta (opcional conforme tipo)
    public string? CounterpartKey { get; set; }          // Chave normalizada da contraparte
    public decimal Valor { get; set; }
    public DateTime DataTransacao { get; set; }
    public string? Descricao { get; set; }

    // Campos extras para EMITIDAS
    public DateTime? DataVencimento { get; set; }
    public string? StatusTitulo { get; set; }            // ABERTO | PAGO | CANCELADO | VENCIDO

    // Campos extras para TRANSF
    public string? ContaOrigem { get; set; }
    public string? ContaDestino { get; set; }

    // Campos extras para COMP
    public string? Categoria { get; set; }

    // Controle
    public string Status { get; set; } = "ATIVO";        // ATIVO | CANCELADO
    public string? ProcessedBy_Id { get; set; }          // FK IdentityUser
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
