using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Domain.Entities;
using AlufranFinConsole.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlufranFinConsole.Tests;

/// <summary>
/// Teste de integração: garante que todo FinancialFact gerado pelo pipeline
/// possui SourceStagingRow_Id > 0 (FK real para uma StagingRow persistida).
///
/// Invariante central — spec §19:
/// "FinancialFact.SourceStagingRow_Id DEVE referenciar a StagingRow de origem;
///  valor 0 indica bug de sequência de persistência."
/// </summary>
public class FinancialSanitizationServiceIntegrationTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Pipeline completo com 3 linhas PAG válidas:
    /// após execução, cada FinancialFact deve ter SourceStagingRow_Id > 0
    /// e deve apontar para uma StagingRow realmente persistida.
    /// </summary>
    [Fact]
    public async Task EveryFinancialFact_HasSourceStagingRowId_GreaterThanZero()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        await using var ctx = CreateInMemoryContext();

        // Categoria ERP com chave normalizada (sem acentos, uppercase)
        ctx.ErpCategories.Add(new ErpCategory
        {
            Code           = "000105",
            Name           = "MATERIA PRIMA",
            ErpCategoryKey = "000105 MATERIA PRIMA",   // chave normalizada — NormalizeKey()
            DreGroup       = "CUSTO",
            DreSubgroup    = "CMV",
            DreOrder       = 10,
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Seed ColumnMappings padrão (idempotente — usa SeedDefaultMappingsAsync)
        var mappingSvc = new ColumnMappingService(ctx,
            NullLogger<ColumnMappingService>.Instance);
        await mappingSvc.SeedDefaultMappingsAsync();

        // ImportFile + FileVersion
        var importFile = new ImportFile
        {
            FileName       = "test_pag.xlsx",
            FileHash       = "deadbeef0001",
            FileType       = "PAG",
            Competence     = "2026-01",
            Status         = "PROCESSED",
            StoragePath    = "test_pag.xlsx",
            UploadedBy_Id  = "test-user",
            CreatedAt      = DateTime.UtcNow
        };
        ctx.ImportFiles.Add(importFile);
        await ctx.SaveChangesAsync();

        var fv = new FileVersion
        {
            ImportFile_Id = importFile.Id,
            VersionNumber = 1,
            FileHash      = "deadbeef0001",
            StoragePath   = "test_pag.xlsx",
            Status        = "PROCESSED",
            CreatedAt     = DateTime.UtcNow
        };
        ctx.FileVersions.Add(fv);
        await ctx.SaveChangesAsync();

        // 3 linhas PAG válidas:
        //   chaves JSON = SourceColumnName dos ColumnMappings → mapeiam para domínio
        //   "Fornecedor"   → SupplierName  (req)
        //   "categoria_erp"→ ErpCategoryName (req)
        //   "valor"        → TitleAmount  (req, decimal)
        //   "filial"       → CompanyName
        //   "unidade"      → UnitName
        //   "numero_nf"    → DocumentNumber
        for (int i = 1; i <= 3; i++)
        {
            ctx.StagingData.Add(new StagingData
            {
                ImportFile_Id     = importFile.Id,
                LineNumber        = i,
                RawData           = "",
                ParsedData        =
                    $$$"""{"Fornecedor":"ORION SA","categoria_erp":"000105 MATERIA PRIMA","valor":"{{{i * 1000}}}","filial":"EMP1","unidade":"UN1","numero_nf":"DOC-{{{i:000}}}"}""",
                ValidationStatus  = "PENDING",
                ValidationErrors  = "",   // required by InMemory nullability check
                SanitizedData     = "",   // required by InMemory nullability check
                CreatedAt         = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync();

        // Instanciar todos os serviços sem mocks (integração real)
        var norm     = new TextNormalizationService();
        var discard  = new DiscardService();
        var qaService= new QaIssueService();
        var classify = new ClassificationService(ctx, norm);
        var svc      = new FinancialSanitizationService(
            ctx, mappingSvc, norm, discard, qaService, classify,
            NullLogger<FinancialSanitizationService>.Instance);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await svc.RunAsync(fv.Id, "test-user");

        // ── Assert ────────────────────────────────────────────────────────────
        result.FactsGenerated.Should().BeGreaterThan(0,
            "linhas PAG válidas e classificadas devem gerar FinancialFacts");

        var facts = await ctx.FinancialFacts.ToListAsync();
        facts.Should().NotBeEmpty(
            "pipeline com dados válidos deve produzir FinancialFacts");

        // Invariante principal: nenhum SourceStagingRow_Id pode ser 0
        facts.Should().AllSatisfy(f =>
            f.SourceStagingRow_Id.Should().BeGreaterThan(0,
                $"FinancialFact Id={f.Id} deve ter SourceStagingRow_Id > 0; " +
                $"valor 0 indica bug de ordem de persistência"));

        // Integridade referencial: cada FK aponta para uma StagingRow que existe
        var stagingRowIds = await ctx.StagingRows
            .Select(s => s.Id)
            .ToListAsync();

        stagingRowIds.Should().NotBeEmpty(
            "StagingRows devem ser persistidas antes dos FinancialFacts");

        facts.Select(f => f.SourceStagingRow_Id)
             .Should().BeSubsetOf(stagingRowIds,
                "cada FinancialFact.SourceStagingRow_Id deve referenciar " +
                "uma StagingRow existente no banco");
    }
}
