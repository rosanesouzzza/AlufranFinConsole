using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AlufranFinConsole.Domain.Entities;

namespace AlufranFinConsole.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // Note: IdentityDbContext already provides DbSet<IdentityUser> Users

    // Files & Versioning
    public DbSet<ImportFile> ImportFiles { get; set; }
    public DbSet<StagingData> StagingData { get; set; }

    // Cadastros (Master Data)
    public DbSet<Company> Companies { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
    public DbSet<ErpCategory> ErpCategories { get; set; }
    public DbSet<ClassificationRule> ClassificationRules { get; set; }
    public DbSet<ColumnMapping> ColumnMappings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Company
        modelBuilder.Entity<Company>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.CNPJ).HasMaxLength(14);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.Code).IsUnique();
        });

        // Unit
        modelBuilder.Entity<Unit>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Type).HasMaxLength(30);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.Company_Id);
        });

        // Supplier
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

        // Client
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

        // Service
        modelBuilder.Entity<Service>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ServiceKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ServiceKey).IsUnique();
        });

        // Product
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ProductKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ProductKey).IsUnique();
        });

        // ChartOfAccount
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

        // ErpCategory
        modelBuilder.Entity<ErpCategory>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.ErpCategoryKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.HasIndex(x => x.ErpCategoryKey).IsUnique();
        });

        // ClassificationRule
        modelBuilder.Entity<ClassificationRule>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.RuleType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Condition).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.Result).IsRequired().HasColumnType("TEXT");
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy_Id).HasMaxLength(450); // IdentityUser Id length
        });

        // ColumnMapping
        modelBuilder.Entity<ColumnMapping>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FileType).IsRequired().HasMaxLength(20);
            b.Property(x => x.SourceColumn).IsRequired().HasMaxLength(256);
            b.Property(x => x.TargetColumn).IsRequired().HasMaxLength(256);
            b.Property(x => x.DataType).IsRequired().HasMaxLength(50);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ImportFile
        modelBuilder.Entity<ImportFile>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            b.Property(x => x.FileHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.FileType).IsRequired().HasMaxLength(20);
            b.Property(x => x.Competence).IsRequired().HasMaxLength(7);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.StoragePath).IsRequired();
            b.Property(x => x.UploadedBy_Id).HasMaxLength(450); // IdentityUser Id length
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // StagingData
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
    }
}
