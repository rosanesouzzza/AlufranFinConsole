using System.Globalization;
using System.Text.Json;
using AlufranFinConsole.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlufranFinConsole.Application.Services;

public class ColumnMappingService : IColumnMappingService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ColumnMappingService> _logger;

    public ColumnMappingService(IApplicationDbContext context, ILogger<ColumnMappingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ColumnMapping>> GetMappingsAsync(string baseType, CancellationToken ct = default)
    {
        return await _context.ColumnMappings
            .Where(m => m.BaseType == baseType.ToUpperInvariant() && m.IsActive)
            .ToListAsync(ct);
    }

    public MappingResult Apply(
        IReadOnlyList<ColumnMapping> mappings,
        IReadOnlyDictionary<string, string> rawRow)
    {
        var normalized   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing      = new List<string>();
        var dropped      = new List<string>();

        // Build lookup: SourceColumnName (upper, trimmed) → mapping.
        // GroupBy handles any duplicate source keys gracefully — first entry wins.
        var mapLookup = mappings
            .GroupBy(m => m.SourceColumnName.Trim().ToUpperInvariant(),
                     StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(),
                          StringComparer.OrdinalIgnoreCase);

        // Apply known mappings
        foreach (var (srcCol, rawValue) in rawRow)
        {
            var srcUpper = srcCol.Trim().ToUpperInvariant();
            if (mapLookup.TryGetValue(srcUpper, out var mapping))
            {
                if (!mapping.ShouldKeep) { dropped.Add(srcCol); continue; }

                var transformed = ApplyTransformation(rawValue, mapping);
                normalized[mapping.TargetColumnName] = transformed;
            }
            else
            {
                dropped.Add(srcCol);   // coluna não mapeada → descartada do normalized
            }
        }

        // Check required columns
        foreach (var m in mappings.Where(m => m.IsRequired && m.ShouldKeep))
        {
            if (!normalized.ContainsKey(m.TargetColumnName))
                missing.Add(m.TargetColumnName);
        }

        return new MappingResult(normalized, missing, dropped);
    }

    private static string ApplyTransformation(string value, ColumnMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.TransformationRule)) return value?.Trim() ?? "";

        try
        {
            using var doc = JsonDocument.Parse(mapping.TransformationRule);
            var root = doc.RootElement;
            var v    = value ?? "";

            if (root.TryGetProperty("trim", out var trim) && trim.GetBoolean())
                v = v.Trim();

            if (root.TryGetProperty("upper", out var upper) && upper.GetBoolean())
                v = v.ToUpperInvariant();

            if (root.TryGetProperty("lower", out var lower) && lower.GetBoolean())
                v = v.ToLowerInvariant();

            return v;
        }
        catch
        {
            return value?.Trim() ?? "";
        }
    }

    public async Task SeedDefaultMappingsAsync(CancellationToken ct = default)
    {
        if (await _context.ColumnMappings.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var seeds = DefaultMappings(now);
        _context.ColumnMappings.AddRange(seeds);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} default ColumnMappings", seeds.Count);
    }

    private static List<ColumnMapping> DefaultMappings(DateTime now)
    {
        ColumnMapping M(string baseType, string src, string tgt, string dt = "string",
            bool req = false, bool keep = true, string? rule = null) =>
            new()
            {
                BaseType           = baseType,
                SourceColumnName   = src,
                TargetColumnName   = tgt,
                DataType           = dt,
                IsRequired         = req,
                ShouldKeep         = keep,
                TransformationRule = rule,
                IsActive           = true,
                CreatedAt          = now
            };

        return
        [
            // ── PAG ─────────────────────────────────────────────────────────
            // Standard canonical column names (ERP exports with proper headers)
            M("PAG","Empresa","CompanyName"),
            M("PAG","Unidade","UnitName"),
            M("PAG","Fornecedor","SupplierName",req:true),
            M("PAG","CNPJ Fornecedor","SupplierDocument"),
            M("PAG","Categoria ERP","ErpCategoryName",req:true),
            M("PAG","Número Documento","DocumentNumber",req:true),
            M("PAG","Data Emissão","IssueDate",dt:"datetime"),
            M("PAG","Data Vencimento","DueDate",dt:"datetime"),
            M("PAG","Data Pagamento","PaymentDate",dt:"datetime"),
            M("PAG","Valor Título","TitleAmount",dt:"decimal",req:true),
            M("PAG","Valor Pago","PaidAmount",dt:"decimal"),
            M("PAG","Valor Aberto","OpenAmount",dt:"decimal"),
            M("PAG","Status Título","TitleStatus"),
            M("PAG","Histórico","History"),
            // Internal field names (underscore-based) from FileRowExtractor — BD_PAG real file
            // Note: "unidade" is deliberately omitted — covered by "Unidade" above (case-insensitive)
            M("PAG","filial","CompanyName"),
            M("PAG","categoria_erp","ErpCategoryName"),
            M("PAG","valor","TitleAmount",dt:"decimal",req:true),
            M("PAG","data_vencimento","DueDate",dt:"datetime"),
            M("PAG","data_entrada","IssueDate",dt:"datetime"),
            M("PAG","numero_nf","DocumentNumber"),
            M("PAG","vl_rateio","VlRateio",keep:false),
            M("PAG","mes_vencimento","MesVencimento",keep:false),
            M("PAG","grupo_dre","GrupoDre",keep:false),
            M("PAG","subgrupo","Subgrupo",keep:false),

            // ── REC ─────────────────────────────────────────────────────────
            M("REC","Empresa","CompanyName"),
            M("REC","Unidade","UnitName"),
            M("REC","Cliente","ClientName",req:true),
            M("REC","CNPJ Cliente","ClientDocument"),
            M("REC","Categoria ERP","ErpCategoryName",req:true),
            M("REC","Número Documento","DocumentNumber",req:true),
            M("REC","Data Emissão","IssueDate",dt:"datetime"),
            M("REC","Data Vencimento","DueDate",dt:"datetime"),
            M("REC","Data Recebimento","ReceiptDate",dt:"datetime"),
            M("REC","Valor Título","TitleAmount",dt:"decimal",req:true),
            M("REC","Valor Recebido","ReceivedAmount",dt:"decimal"),
            M("REC","Valor Aberto","OpenAmount",dt:"decimal"),
            M("REC","Status Título","TitleStatus"),
            M("REC","Histórico","History"),

            // ── FAT ─────────────────────────────────────────────────────────
            M("FAT","Empresa","CompanyName"),
            M("FAT","Unidade","UnitName"),
            M("FAT","Cliente","ClientName",req:true),
            M("FAT","CNPJ Cliente","ClientDocument"),
            M("FAT","Serviço","ServiceName"),
            M("FAT","Produto","ProductName"),
            M("FAT","Documento Faturamento","BillingDocument"),
            M("FAT","Data Faturamento","BillingDate",dt:"datetime"),
            M("FAT","Quantidade","Quantity",dt:"decimal"),
            M("FAT","Valor Unitário","UnitAmount",dt:"decimal"),
            M("FAT","Valor Total","TotalAmount",dt:"decimal",req:true),
            M("FAT","Categoria ERP","ErpCategoryName"),
            M("FAT","Histórico","History"),

            // ── EMITIDAS ─────────────────────────────────────────────────────
            M("EMITIDAS","Empresa","CompanyName"),
            M("EMITIDAS","Unidade","UnitName"),
            M("EMITIDAS","Cliente","ClientName",req:true),
            M("EMITIDAS","CNPJ Cliente","ClientDocument"),
            M("EMITIDAS","Número NF","InvoiceNumber",req:true),
            M("EMITIDAS","Série","InvoiceSeries"),
            M("EMITIDAS","Data Emissão","IssueDate",dt:"datetime",req:true),
            M("EMITIDAS","Data Competência","CompetenceDate",dt:"datetime"),
            M("EMITIDAS","Valor Bruto","GrossAmount",dt:"decimal",req:true),
            M("EMITIDAS","Valor Líquido","NetAmount",dt:"decimal"),
            M("EMITIDAS","Valor Imposto","TaxAmount",dt:"decimal"),
            M("EMITIDAS","Status NF","InvoiceStatus"),
            M("EMITIDAS","Serviço","ServiceName"),
            M("EMITIDAS","Produto","ProductName"),
            M("EMITIDAS","Categoria ERP","ErpCategoryName"),
            M("EMITIDAS","Histórico","History"),

            // ── COMP ──────────────────────────────────────────────────────────
            M("COMP","Empresa","CompanyName"),
            M("COMP","Unidade","UnitName"),
            M("COMP","Fornecedor","SupplierName",req:true),
            M("COMP","CNPJ Fornecedor","SupplierDocument"),
            M("COMP","Produto","ProductName"),
            M("COMP","Grupo Produto","ProductGroupName"),
            M("COMP","Categoria ERP","ErpCategoryName",req:true),
            M("COMP","Documento Compra","PurchaseDocument"),
            M("COMP","Data Emissão","IssueDate",dt:"datetime"),
            M("COMP","Data Competência","CompetenceDate",dt:"datetime"),
            M("COMP","Quantidade","Quantity",dt:"decimal"),
            M("COMP","Valor Unitário","UnitAmount",dt:"decimal"),
            M("COMP","Valor Total","TotalAmount",dt:"decimal",req:true),
            M("COMP","Histórico","History"),

            // ── TRANSF ────────────────────────────────────────────────────────
            M("TRANSF","Empresa","CompanyName"),
            M("TRANSF","Unidade","UnitName"),
            M("TRANSF","Conta Origem","SourceAccount",req:true),
            M("TRANSF","Conta Destino","DestinationAccount",req:true),
            M("TRANSF","Empresa Destino","DestinationCompanyName"),
            M("TRANSF","Unidade Destino","DestinationUnitName"),
            M("TRANSF","Documento","TransferDocument"),
            M("TRANSF","Data Transferência","TransferDate",dt:"datetime",req:true),
            M("TRANSF","Valor","TransferAmount",dt:"decimal",req:true),
            M("TRANSF","Tipo","TransferType"),
            M("TRANSF","Histórico","History"),

            // ── FOPAG ────────────────────────────────────────────────────────
            M("FOPAG","Empresa","CompanyName"),
            M("FOPAG","Unidade","UnitName"),
            M("FOPAG","Centro de Custo","CostCenterName"),
            M("FOPAG","Matrícula","EmployeeExternalId",req:true),
            M("FOPAG","Cargo / Função","RoleName"),
            M("FOPAG","Rubrica","PayrollItemName",req:true),
            M("FOPAG","Tipo Rubrica","PayrollItemType"),
            M("FOPAG","Competência","CompetenceDate",dt:"datetime",req:true),
            M("FOPAG","Valor","PayrollAmount",dt:"decimal",req:true),
            M("FOPAG","Histórico","History"),
        ];
    }
}
