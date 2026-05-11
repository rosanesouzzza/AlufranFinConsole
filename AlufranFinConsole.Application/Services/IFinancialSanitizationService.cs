using AlufranFinConsole.Domain.Entities;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Orquestra os 21 passos do pipeline de saneamento financeiro — spec §4.
/// Converte linhas brutas de um FileVersion em StagingRows, DiscardedRows, QaIssues e FinancialFacts.
/// </summary>
public interface IFinancialSanitizationService
{
    Task<ProcessingRunResult> RunAsync(
        int fileVersionId,
        string startedByUserId,
        CancellationToken ct = default);
}

public sealed record ProcessingRunResult(
    int ProcessingRunId,
    int TotalRows,
    int ValidRows,
    int DiscardedRows,
    int QaRows,
    int FactsGenerated,
    bool HasBlockingQa,
    string Status);
