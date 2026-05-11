using AlufranFinConsole.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Interface de persistência exposta pela camada Application.
/// Permite que serviços de aplicação dependam de abstração, não de implementação concreta.
/// </summary>
public interface IApplicationDbContext
{
    // ── Cadastros / Master Data ──────────────────────────────────────────────
    DbSet<Company>           Companies          { get; }
    DbSet<Unit>              Units              { get; }
    DbSet<Supplier>          Suppliers          { get; }
    DbSet<Client>            Clients            { get; }
    DbSet<Service>           Services           { get; }
    DbSet<Product>           Products           { get; }
    DbSet<ChartOfAccount>    ChartOfAccounts    { get; }
    DbSet<ErpCategory>       ErpCategories      { get; }
    DbSet<ClassificationRule> ClassificationRules { get; }
    DbSet<ColumnMapping>     ColumnMappings     { get; }

    // ── Arquivo e Versionamento ──────────────────────────────────────────────
    DbSet<ImportFile>        ImportFiles        { get; }
    DbSet<FileVersion>       FileVersions       { get; }

    // ── Pipeline ─────────────────────────────────────────────────────────────
    DbSet<ProcessingRun>     ProcessingRuns     { get; }
    DbSet<StagingRow>        StagingRows        { get; }
    DbSet<DiscardedRow>      DiscardedRows      { get; }
    DbSet<QaIssue>           QaIssues           { get; }
    DbSet<FinancialFact>     FinancialFacts     { get; }

    // ── Legacy (compatibilidade) ──────────────────────────────────────────────
    DbSet<StagingData>       StagingData        { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<PayrollEntry>      PayrollEntries     { get; }
    DbSet<ClosingApproval>   ClosingApprovals   { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
