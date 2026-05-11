using AlufranFinConsole.Domain.Entities;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Aplica ColumnMappings ao payload bruto de uma linha.
/// Regra central: nenhuma base mapeia coluna direto no código — spec §6.
/// </summary>
public interface IColumnMappingService
{
    /// <summary>
    /// Busca os mapeamentos ativos para o baseType.
    /// </summary>
    Task<IReadOnlyList<ColumnMapping>> GetMappingsAsync(string baseType, CancellationToken ct = default);

    /// <summary>
    /// Transforma o dicionário bruto (nomes de coluna do arquivo) em dicionário normalizado
    /// (nomes canônicos internos), descartando colunas não mapeadas e retornando
    /// os campos ausentes obrigatórios para geração de QA.
    /// </summary>
    MappingResult Apply(
        IReadOnlyList<ColumnMapping> mappings,
        IReadOnlyDictionary<string, string> rawRow);

    /// <summary>Seed: insere os mapeamentos padrão caso a tabela esteja vazia.</summary>
    Task SeedDefaultMappingsAsync(CancellationToken ct = default);
}

public sealed record MappingResult(
    Dictionary<string, string> NormalizedRow,
    List<string> MissingRequiredColumns,
    List<string> DroppedColumns);
