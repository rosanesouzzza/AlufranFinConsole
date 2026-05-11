using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AlufranFinConsole.Tests;

public class ColumnMappingServiceTests
{
    private static readonly IReadOnlyList<ColumnMapping> PagMappings =
    [
        new() { BaseType="PAG", SourceColumnName="Fornecedor",    TargetColumnName="SupplierName", DataType="string", IsRequired=true,  ShouldKeep=true,  IsActive=true, CreatedAt=DateTime.UtcNow },
        new() { BaseType="PAG", SourceColumnName="Valor Título",  TargetColumnName="TitleAmount",  DataType="decimal",IsRequired=true,  ShouldKeep=true,  IsActive=true, CreatedAt=DateTime.UtcNow },
        new() { BaseType="PAG", SourceColumnName="Categoria ERP", TargetColumnName="ErpCategoryName",DataType="string",IsRequired=true, ShouldKeep=true,  IsActive=true, CreatedAt=DateTime.UtcNow },
        new() { BaseType="PAG", SourceColumnName="Cod Interno",   TargetColumnName="InternalCode", DataType="string", IsRequired=false, ShouldKeep=false, IsActive=true, CreatedAt=DateTime.UtcNow },
    ];

    private readonly IApplicationDbContext _ctx = Substitute.For<IApplicationDbContext>();
    private readonly ILogger<ColumnMappingService> _log = Substitute.For<ILogger<ColumnMappingService>>();
    private ColumnMappingService Sut() => new(_ctx, _log);

    [Fact]
    public void MappedColumn_AppearsInNormalized()
    {
        var raw  = new Dictionary<string, string> { ["Fornecedor"] = "ORION" };
        var res  = Sut().Apply(PagMappings, raw);
        res.NormalizedRow.Should().ContainKey("SupplierName");
        res.NormalizedRow["SupplierName"].Should().Be("ORION");
    }

    [Fact]
    public void AbsentRequiredColumn_ReportedInMissing()
    {
        var raw = new Dictionary<string, string> { ["Fornecedor"] = "ORION" };  // TitleAmount e ErpCategoryName ausentes
        var res = Sut().Apply(PagMappings, raw);
        res.MissingRequiredColumns.Should().Contain("TitleAmount");
        res.MissingRequiredColumns.Should().Contain("ErpCategoryName");
    }

    [Fact]
    public void ExtraUnmappedColumn_IsDropped()
    {
        var raw = new Dictionary<string, string>
        {
            ["Fornecedor"]   = "ORION",
            ["Coluna Extra"] = "IGNORAR"
        };
        var res = Sut().Apply(PagMappings, raw);
        res.DroppedColumns.Should().Contain("Coluna Extra");
        res.NormalizedRow.Should().NotContainKey("Coluna Extra");
    }

    [Fact]
    public void ShouldKeepFalse_ColumnDropped()
    {
        var raw = new Dictionary<string, string>
        {
            ["Fornecedor"]  = "ORION",
            ["Cod Interno"] = "999"
        };
        var res = Sut().Apply(PagMappings, raw);
        res.NormalizedRow.Should().NotContainKey("InternalCode");
        res.DroppedColumns.Should().Contain("Cod Interno");
    }

    [Fact]
    public void CaseInsensitiveHeaderMatch()
    {
        var raw = new Dictionary<string, string> { ["FORNECEDOR"] = "ORION" };
        var res = Sut().Apply(PagMappings, raw);
        res.NormalizedRow.Should().ContainKey("SupplierName");
    }
}
