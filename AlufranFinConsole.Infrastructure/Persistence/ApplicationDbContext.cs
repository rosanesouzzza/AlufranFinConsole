using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AlufranFinConsole.Domain.Entities;
using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // ── Cadastros / Master Data ──────────────────────────────────────────────
    public DbSet<Company>            Companies           { get; set; }
    public DbSet<Unit>               Units               { get; set; }
    public DbSet<Supplier>           Suppliers           { get; set; }
    public DbSet<Client>             Clients             { get; set; }
    public DbSet<Service>            Services            { get; set; }
    public DbSet<Product>            Products            { get; set; }
    public DbSet<ChartOfAccount>     ChartOfAccounts     { get; set; }
    public DbSet<ErpCategory>        ErpCategories       { get; set; }
    public DbSet<ClassificationRule> ClassificationRules { get; set; }
    public DbSet<ColumnMapping>      ColumnMappings      { get; set; }

    // ── Arquivo e Versionamento ──────────────────────────────────────────────
    public DbSet<ImportFile>   ImportFiles   { get; set; }
    public DbSet<FileVersion>  FileVersions  { get; set; }

    // ── Pipeline novo ────────────────────────────────────────────────────────
    public DbSet<ProcessingRun>  ProcessingRuns  { get; set; }
    public DbSet<StagingRow>     StagingRows     { get; set; }
    public DbSet<DiscardedRow>   DiscardedRows   { get; set; }
    public DbSet<QaIssue>        QaIssues        { get; set; }
    public DbSet<FinancialFact>  FinancialFacts  { get; set; }

    // ── Legacy (compatibilidade) ──────────────────────────────────────────────
    public DbSet<StagingData>           StagingData           { get; set; }
    public DbSet<FinancialTransaction>  FinancialTransactions { get; set; }
    public DbSet<PayrollEntry>          PayrollEntries        { get; set; }
    public DbSet<ClosingApproval>       ClosingApprovals      { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Company ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Company>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.CNPJ).HasMaxLength(14);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.Code).IsUnique();
        });

        // ── Unit ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Unit>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Type).HasMaxLength(30);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.Company_Id);
        });

        // ── Supplier ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Supplier>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.CNPJ).HasMaxLength(14);
            b.Property(x => x.SupplierKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.SupplierKey).IsUnique();
        });

        // ── Client ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.CNPJ).HasMaxLength(14);
            b.Property(x => x.ClientKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ClientKey).IsUnique();
        });

        // ── Service ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Service>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ServiceKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ServiceKey).IsUnique();
        });

        // ── Product ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ProductKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ProductKey).IsUnique();
        });

        // ── ChartOfAccount ───────────────────────────────────────────────────
        modelBuilder.Entity<ChartOfAccount>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Type).IsRequired().HasMaxLength(30);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.Parent_Id);
            b.HasIndex(x => x.Number).IsUnique();
        });

        // ── ErpCategory ──────────────────────────────────────────────────────
        modelBuilder.Entity<ErpCategory>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ErpCategoryKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.DreGroup).HasMaxLength(100);
            b.Property(x => x.DreSubgroup).HasMaxLength(100);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ErpCategoryKey).IsUnique();
        });

        // ── ClassificationRule ───────────────────────────────────────────────
        modelBuilder.Entity<ClassificationRule>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.RuleType).IsRequired().HasMaxLength(50);
            b.Property(x => x.BaseType).HasMaxLength(20).HasDefaultValue("*");
            b.Property(x => x.Condition).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.Result).HasColumnType("TEXT");
            b.Property(x => x.DreGroup).HasMaxLength(100);
            b.Property(x => x.DreSubgroup).HasMaxLength(100);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy_Id).HasMaxLength(450);
        });

        // ── ColumnMapping ────────────────────────────────────────────────────
        modelBuilder.Entity<ColumnMapping>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.SourceColumnName).IsRequired().HasMaxLength(256);
            b.Property(x => x.TargetColumnName).IsRequired().HasMaxLength(256);
            b.Property(x => x.DataType).IsRequired().HasMaxLength(50);
            b.Property(x => x.TransformationRule).HasColumnType("TEXT");
            b.Property(x => x.Description).HasMaxLength(512);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => new { x.BaseType, x.SourceColumnName });
        });

        // ── ImportFile ───────────────────────────────────────────────────────
        modelBuilder.Entity<ImportFile>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            b.Property(x => x.FileHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.FileType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.StoragePath).IsRequired();
            b.Property(x => x.UploadedBy_Id).HasMaxLength(450);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ── FileVersion ──────────────────────────────────────────────────────
        modelBuilder.Entity<FileVersion>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FileHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.StoragePath).IsRequired();
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.Notes).HasColumnType("TEXT");
            b.Property(x => x.CreatedBy_Id).HasMaxLength(450);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ImportFile).WithMany().HasForeignKey(x => x.ImportFile_Id);
            b.HasIndex(x => new { x.ImportFile_Id, x.VersionNumber }).IsUnique();
        });

        // ── ProcessingRun ────────────────────────────────────────────────────
        modelBuilder.Entity<ProcessingRun>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.Summary).HasColumnType("TEXT");
            b.Property(x => x.ErrorMessage).HasColumnType("TEXT");
            b.Property(x => x.StartedBy_Id).HasMaxLength(450);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.FileVersion).WithMany().HasForeignKey(x => x.FileVersion_Id);
            b.HasIndex(x => new { x.BaseType, x.Competence });
        });

        // ── StagingRow ───────────────────────────────────────────────────────
        modelBuilder.Entity<StagingRow>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.RawJson).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.NormalizedJson).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.LineHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.LineStatus).IsRequired().HasMaxLength(20);
            b.Property(x => x.StatusReason).HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ProcessingRun).WithMany().HasForeignKey(x => x.ProcessingRun_Id);
            b.HasIndex(x => x.ProcessingRun_Id);
            b.HasIndex(x => x.LineStatus);
            b.HasIndex(x => x.LineHash);
        });

        // ── DiscardedRow ─────────────────────────────────────────────────────
        modelBuilder.Entity<DiscardedRow>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.RawJson).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.DiscardReason).IsRequired().HasMaxLength(50);
            b.Property(x => x.DiscardDetail).HasMaxLength(512);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ProcessingRun).WithMany().HasForeignKey(x => x.ProcessingRun_Id);
            b.HasIndex(x => x.ProcessingRun_Id);
            b.HasIndex(x => x.DiscardReason);
        });

        // ── QaIssue ──────────────────────────────────────────────────────────
        modelBuilder.Entity<QaIssue>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.IssueType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Severity).IsRequired().HasMaxLength(20);
            b.Property(x => x.Message).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.RawJson).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.NormalizedJson).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ProcessingRun).WithMany().HasForeignKey(x => x.ProcessingRun_Id);
            b.HasIndex(x => x.ProcessingRun_Id);
            b.HasIndex(x => new { x.Severity, x.Status });
        });

        // ── FinancialFact ────────────────────────────────────────────────────
        modelBuilder.Entity<FinancialFact>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.BaseType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.DocumentNumber).HasMaxLength(100);
            b.Property(x => x.AmountCompetence).HasColumnType("decimal(18,4)");
            b.Property(x => x.AmountCash).HasColumnType("decimal(18,4)");
            b.Property(x => x.DreGroup).HasMaxLength(100);
            b.Property(x => x.DreSubgroup).HasMaxLength(100);
            b.Property(x => x.ClassificationStatus).IsRequired().HasMaxLength(30);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ProcessingRun).WithMany().HasForeignKey(x => x.ProcessingRun_Id);
            b.HasIndex(x => new { x.Competence, x.BaseType });
            b.HasIndex(x => x.DreGroup);
        });

        // ── StagingData (legado) ─────────────────────────────────────────────
        modelBuilder.Entity<StagingData>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.LineNumber).IsRequired();
            b.Property(x => x.RawData).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.ParsedData).HasColumnType("TEXT");
            b.Property(x => x.ValidationStatus).IsRequired().HasMaxLength(20);
            b.Property(x => x.ValidationErrors).HasColumnType("TEXT");
            b.Property(x => x.SanitizedData).HasColumnType("TEXT");
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ImportFile).WithMany().HasForeignKey(x => x.ImportFile_Id);
            b.HasIndex(x => x.ImportFile_Id);
            b.HasIndex(x => x.ValidationStatus);
        });

        // ── FinancialTransaction (legado) ─────────────────────────────────────
        modelBuilder.Entity<FinancialTransaction>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TransactionType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Documento).IsRequired().HasMaxLength(100);
            b.Property(x => x.Counterpart).HasMaxLength(512);
            b.Property(x => x.CounterpartKey).HasMaxLength(512);
            b.Property(x => x.Valor).HasColumnType("decimal(18,2)");
            b.Property(x => x.Descricao).HasColumnType("TEXT");
            b.Property(x => x.StatusTitulo).HasMaxLength(20);
            b.Property(x => x.ContaOrigem).HasMaxLength(50);
            b.Property(x => x.ContaDestino).HasMaxLength(50);
            b.Property(x => x.Categoria).HasMaxLength(100);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("ATIVO");
            b.Property(x => x.ProcessedBy_Id).HasMaxLength(450);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ImportFile).WithMany().HasForeignKey(x => x.ImportFile_Id);
            b.HasOne(x => x.StagingData).WithMany().HasForeignKey(x => x.StagingData_Id);
            b.HasIndex(x => new { x.Competence, x.TransactionType });
            b.HasIndex(x => x.StagingData_Id).IsUnique();
        });

        // ── ClosingApproval ───────────────────────────────────────────────────
        modelBuilder.Entity<ClosingApproval>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.ApprovedBy).IsRequired().HasMaxLength(512);
            b.Property(x => x.Notes).HasColumnType("TEXT");
            b.Property(x => x.DreSnapshot).HasColumnType("TEXT");
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Ignore(x => x.IsAprovado);
            b.HasIndex(x => x.Competence);
            b.HasIndex(x => new { x.Competence, x.Status });
        });

        // ── PayrollEntry (legado) ─────────────────────────────────────────────
        modelBuilder.Entity<PayrollEntry>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Matricula).IsRequired().HasMaxLength(50);
            b.Property(x => x.Funcionario).IsRequired().HasMaxLength(512);
            b.Property(x => x.FuncionarioKey).HasMaxLength(512);
            b.Property(x => x.Cargo).HasMaxLength(200);
            b.Property(x => x.ValorBruto).HasColumnType("decimal(18,2)");
            b.Property(x => x.Descontos).HasColumnType("decimal(18,2)");
            b.Property(x => x.ValorLiquido).HasColumnType("decimal(18,2)");
            b.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("ATIVO");
            b.Property(x => x.ProcessedBy_Id).HasMaxLength(450);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.ImportFile).WithMany().HasForeignKey(x => x.ImportFile_Id);
            b.HasOne(x => x.StagingData).WithMany().HasForeignKey(x => x.StagingData_Id);
            b.HasIndex(x => new { x.Competence, x.Matricula });
            b.HasIndex(x => x.StagingData_Id).IsUnique();
        });
    }
}
