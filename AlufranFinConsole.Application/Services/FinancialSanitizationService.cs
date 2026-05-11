using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlufranFinConsole.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Implementação dos 21 passos do pipeline de saneamento — spec §4.
///
/// Pipeline (por linha):
///  1  Registrar arquivo (já feito — recebe fileVersionId)
///  2  Criar versão (já existe)
///  3  Hash calculado no upload
///  4  Criar ProcessingRun
///  5  Ler arquivo bruto (StagingData legado → rows)
///  6  Aplicar ColumnMappings
///  7  Padronizar nomes de colunas (via ColumnMappingService)
///  8  Tipar campos (via ColumnMapping.DataType)
///  9  Remover colunas não mapeadas (ShouldKeep=false)
/// 10  Criar campos técnicos
/// 11  Normalizar textos (ITextNormalizationService)
/// 12  Criar chaves oficiais
/// 13  Identificar linhas descartáveis (IDiscardService)
/// 14  Persistir descartes com motivo
/// 15  Identificar linhas suspeitas (IQaIssueService)
/// 16  Persistir QA
/// 17  Aplicar cadastros soberanos (IClassificationService)
/// 18  Aplicar regras de classificação
/// 19  Gerar FinancialFacts somente para linhas válidas e classificadas
/// 20  Gerar resumo do processamento
/// 21  Bloquear aprovação se houver QA Blocking
/// </summary>
public class FinancialSanitizationService : IFinancialSanitizationService
{
    private readonly IApplicationDbContext  _context;
    private readonly IColumnMappingService  _mappingService;
    private readonly ITextNormalizationService _norm;
    private readonly IDiscardService        _discard;
    private readonly IQaIssueService        _qaService;
    private readonly IClassificationService _classify;
    private readonly ILogger<FinancialSanitizationService> _logger;

    public FinancialSanitizationService(
        IApplicationDbContext context,
        IColumnMappingService mappingService,
        ITextNormalizationService norm,
        IDiscardService discard,
        IQaIssueService qaService,
        IClassificationService classify,
        ILogger<FinancialSanitizationService> logger)
    {
        _context        = context;
        _mappingService = mappingService;
        _norm           = norm;
        _discard        = discard;
        _qaService      = qaService;
        _classify       = classify;
        _logger         = logger;
    }

    public async Task<ProcessingRunResult> RunAsync(
        int fileVersionId,
        string startedByUserId,
        CancellationToken ct = default)
    {
        // ── Passo 4: Criar ProcessingRun ─────────────────────────────────────
        var version = await _context.FileVersions
            .Include(v => v.ImportFile)
            .FirstOrDefaultAsync(v => v.Id == fileVersionId, ct)
            ?? throw new InvalidOperationException($"FileVersion {fileVersionId} não encontrada.");

        var run = new ProcessingRun
        {
            FileVersion_Id = fileVersionId,
            BaseType       = version.ImportFile.FileType,
            Competence     = version.ImportFile.Competence,
            Status         = "RUNNING",
            StartedBy_Id   = startedByUserId,
            StartedAt      = DateTime.UtcNow,
            CreatedAt      = DateTime.UtcNow
        };
        _context.ProcessingRuns.Add(run);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ProcessingRun {RunId} iniciado — {BaseType}/{Competence}",
            run.Id, run.BaseType, run.Competence);

        try
        {
            var result = await ExecutePipelineAsync(run, version, ct);
            run.Status         = "COMPLETED";
            run.CompletedAt    = DateTime.UtcNow;
            run.TotalRows      = result.TotalRows;
            run.ValidRows      = result.ValidRows;
            run.DiscardedRows  = result.DiscardedRows;
            run.QaRows         = result.QaRows;
            run.FactsGenerated = result.FactsGenerated;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ProcessingRun {RunId} concluído: {Valid} válidas, {Disc} descartadas, {Qa} QA, {Facts} fatos",
                run.Id, result.ValidRows, result.DiscardedRows, result.QaRows, result.FactsGenerated);

            return result with { ProcessingRunId = run.Id };
        }
        catch (Exception ex)
        {
            run.Status       = "FAILED";
            run.ErrorMessage = ex.Message;
            run.CompletedAt  = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            _logger.LogError(ex, "ProcessingRun {RunId} falhou", run.Id);
            throw;
        }
    }

    // ── Pipeline principal ───────────────────────────────────────────────────

    private async Task<ProcessingRunResult> ExecutePipelineAsync(
        ProcessingRun run,
        FileVersion version,
        CancellationToken ct)
    {
        var baseType   = run.BaseType;
        var competence = run.Competence;

        // ── Passo 6/7: Carregar ColumnMappings ───────────────────────────────
        var mappings = await _mappingService.GetMappingsAsync(baseType, ct);
        _logger.LogDebug("ColumnMappings carregados: {Count} para {BaseType}", mappings.Count, baseType);

        // ── Passo 5: Ler linhas brutas (StagingData legado) ──────────────────
        var stagingRows = await _context.StagingData
            .Where(s => s.ImportFile_Id == version.ImportFile_Id &&
                        s.ValidationStatus == "PENDING")
            .OrderBy(s => s.LineNumber)
            .ToListAsync(ct);

        int totalRows = stagingRows.Count, validRows = 0, discardedRows = 0, qaRows = 0, factsGenerated = 0;
        bool hasBlockingQa = false;

        var batchStagingRows    = new List<StagingRow>(stagingRows.Count);
        var batchDiscarded      = new List<DiscardedRow>();
        var batchQaIssues       = new List<QaIssue>();
        // pendingFacts: coleta (StagingRow, dict normalizado, classificação) durante o loop.
        // FinancialFacts são construídos DEPOIS do primeiro SaveChangesAsync,
        // quando EF Core já atribuiu IDs reais às StagingRows.
        var pendingFacts        = new List<(StagingRow Row, IReadOnlyDictionary<string, string> NormDict, ClassificationResult Cls)>();
        var seenHashes          = new HashSet<string>();

        foreach (var legacyRow in stagingRows)
        {
            // ── Passo 5: Parse RawData → dicionário bruto ─────────────────────
            var rawDict = ParseRawData(legacyRow.RawData, legacyRow.ParsedData);

            // ── Passo 6-9: Aplicar ColumnMappings ────────────────────────────
            var mapResult = _mappingService.Apply(mappings, rawDict);

            // ── Passos 10/11: Campos técnicos + normalização ──────────────────
            var normalizedDict = NormalizeDictionary(mapResult.NormalizedRow);

            // ── Passo 8: Tipar campos (converte datas/números) ───────────────
            ApplyTyping(normalizedDict, mappings);

            // ── Passo 12: Criar chaves oficiais ──────────────────────────────
            AddOfficialKeys(normalizedDict);

            var rawJson        = JsonSerializer.Serialize(rawDict);
            var normalizedJson = JsonSerializer.Serialize(normalizedDict);
            var lineHash       = ComputeHash(normalizedJson);

            // ── Passo 13: Verificar descarte ─────────────────────────────────
            var discardCheck = _discard.Check(normalizedDict, baseType);

            // Duplicidade técnica
            if (!discardCheck.ShouldDiscard && seenHashes.Contains(lineHash))
                discardCheck = new(true, "TechnicalDuplicate");

            if (!discardCheck.ShouldDiscard)
                seenHashes.Add(lineHash);

            // ── Passo 14: Persistir descarte ─────────────────────────────────
            if (discardCheck.ShouldDiscard)
            {
                batchDiscarded.Add(new DiscardedRow
                {
                    ProcessingRun_Id  = run.Id,
                    FileVersion_Id    = version.Id,
                    BaseType          = baseType,
                    Competence        = competence,
                    OriginalRowNumber = legacyRow.LineNumber,
                    RawJson           = rawJson,
                    DiscardReason     = discardCheck.Reason ?? "InvalidStructure",
                    DiscardDetail     = discardCheck.Detail,
                    CreatedAt         = DateTime.UtcNow
                });
                discardedRows++;

                batchStagingRows.Add(BuildStagingRow(run, version, legacyRow, rawJson,
                    normalizedJson, lineHash, "DISCARDED", discardCheck.Reason, baseType, competence));
                continue;
            }

            // ── Passos 15/16: QA ──────────────────────────────────────────────
            var qaChecks = _qaService.Check(normalizedDict, baseType, competence);

            // Colunas obrigatórias ausentes → QA estrutural
            foreach (var missing in mapResult.MissingRequiredColumns)
                qaChecks = [.. qaChecks,
                    new("MissingRequiredField", "Blocking", $"Coluna obrigatória ausente: {missing}")];

            bool rowHasBlockingQa = qaChecks.Any(q => q.Severity == "Blocking");
            if (rowHasBlockingQa) hasBlockingQa = true;

            foreach (var qa in qaChecks)
            {
                batchQaIssues.Add(new QaIssue
                {
                    ProcessingRun_Id  = run.Id,
                    FileVersion_Id    = version.Id,
                    BaseType          = baseType,
                    Competence        = competence,
                    OriginalRowNumber = legacyRow.LineNumber,
                    IssueType         = qa.IssueType,
                    Severity          = qa.Severity,
                    Message           = qa.Message,
                    RawJson           = rawJson,
                    NormalizedJson    = normalizedJson,
                    Status            = "Open",
                    CreatedAt         = DateTime.UtcNow
                });
            }

            var lineStatus = qaChecks.Count > 0 ? "QA" : "VALID";
            if (lineStatus == "QA") qaRows++;
            else validRows++;

            var stagingRow = BuildStagingRow(run, version, legacyRow, rawJson,
                normalizedJson, lineHash, lineStatus, null, baseType, competence);
            batchStagingRows.Add(stagingRow);

            // ── Passos 17/18: Classificação ──────────────────────────────────
            if (lineStatus == "VALID")
            {
                var classification = await _classify.ClassifyAsync(normalizedDict, baseType, ct);

                // ── Passo 19: Enfileirar fato para construção após persistência das StagingRows
                // BuildFinancialFact usa stagingRow.Id que só existe após SaveChangesAsync.
                if (classification.Classified)
                {
                    pendingFacts.Add((stagingRow, normalizedDict, classification));
                    factsGenerated++;
                }
                else
                {
                    // Linha válida mas não classificada → QA adicional
                    hasBlockingQa = true;
                    batchQaIssues.Add(new QaIssue
                    {
                        ProcessingRun_Id  = run.Id,
                        FileVersion_Id    = version.Id,
                        BaseType          = baseType,
                        Competence        = competence,
                        OriginalRowNumber = legacyRow.LineNumber,
                        IssueType         = "UnclassifiedCategory",
                        Severity          = "Blocking",
                        Message           = classification.UnclassifiedReason ?? "Não classificado",
                        RawJson           = rawJson,
                        NormalizedJson    = normalizedJson,
                        Status            = "Open",
                        CreatedAt         = DateTime.UtcNow
                    });
                }
            }
        }

        // ── Passo 20: Persistir em duas fases (garante SourceStagingRow_Id > 0) ─────────────────
        // Fase 1 — StagingRows + DiscardedRows + QaIssues.
        // Após SaveChangesAsync EF Core atribui IDs reais às StagingRow entities.
        if (batchStagingRows.Count > 0) _context.StagingRows.AddRange(batchStagingRows);
        if (batchDiscarded.Count > 0)   _context.DiscardedRows.AddRange(batchDiscarded);
        if (batchQaIssues.Count > 0)    _context.QaIssues.AddRange(batchQaIssues);
        await _context.SaveChangesAsync(ct); // stagingRow.Id agora > 0

        // Fase 2 — FinancialFacts construídos com stagingRow.Id real.
        var batchFacts = pendingFacts
            .Select(p => BuildFinancialFact(run, p.Row, p.NormDict, baseType, competence, p.Cls))
            .ToList();
        if (batchFacts.Count > 0) _context.FinancialFacts.AddRange(batchFacts);
        await _context.SaveChangesAsync(ct);

        return new ProcessingRunResult(
            ProcessingRunId: run.Id,
            TotalRows:       totalRows,
            ValidRows:       validRows,
            DiscardedRows:   discardedRows,
            QaRows:          qaRows,
            FactsGenerated:  factsGenerated,
            HasBlockingQa:   hasBlockingQa,
            Status:          "COMPLETED");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseRawData(string rawData, string? parsedDataJson)
    {
        // Se o ParsedData já veio preenchido (upload XLSX/PDF), usa direto
        if (!string.IsNullOrWhiteSpace(parsedDataJson) && parsedDataJson.StartsWith('{'))
        {
            try
            {
                var fromJson = JsonSerializer.Deserialize<Dictionary<string, string>>(parsedDataJson);
                if (fromJson != null && fromJson.Count > 0) return fromJson;
            }
            catch { /* fallback para CSV */ }
        }

        // CSV: divide por vírgula
        var parts  = rawData.Split(',');
        var dict   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < parts.Length; i++)
            dict[$"col{i+1}"] = parts[i].Trim();
        return dict;
    }

    private Dictionary<string, string> NormalizeDictionary(Dictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in source)
            result[k] = _norm.NormalizeKey(v) ?? v.Trim();
        return result;
    }

    private static void ApplyTyping(Dictionary<string, string> row, IReadOnlyList<ColumnMapping> mappings)
    {
        foreach (var mapping in mappings.Where(m => m.DataType != "string" && m.ShouldKeep))
        {
            if (!row.TryGetValue(mapping.TargetColumnName, out var val)) continue;

            row[mapping.TargetColumnName] = mapping.DataType switch
            {
                "datetime" when DateTime.TryParse(val, out var d)
                    => d.ToString("yyyy-MM-dd"),
                "decimal" when decimal.TryParse(val,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var dec)
                    => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => val
            };
        }
    }

    private void AddOfficialKeys(Dictionary<string, string> row)
    {
        void AddKey(string srcField, string keyField)
        {
            if (row.TryGetValue(srcField, out var v))
                row[keyField] = _norm.NormalizeKey(v) ?? "";
        }

        AddKey("CompanyName",    "CompanyKey");
        AddKey("UnitName",       "UnitKey");
        AddKey("SupplierName",   "SupplierKey");
        AddKey("ClientName",     "ClientKey");
        AddKey("ServiceName",    "ServiceKey");
        AddKey("ProductName",    "ProductKey");
        AddKey("ErpCategoryName","ErpCategoryKey");
        AddKey("DocumentNumber", "DocumentKey");
    }

    private static string ComputeHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(SHA256.HashData(bytes))[..16];
    }

    private static StagingRow BuildStagingRow(
        ProcessingRun run, FileVersion version, StagingData legacy,
        string rawJson, string normalizedJson, string lineHash,
        string lineStatus, string? statusReason,
        string baseType, string competence)
    => new()
    {
        ProcessingRun_Id  = run.Id,
        BaseType          = baseType,
        Competence        = competence,
        ImportFileId      = version.ImportFile_Id,
        FileVersionId     = version.Id,
        OriginalRowNumber = legacy.LineNumber,
        RawJson           = rawJson,
        NormalizedJson    = normalizedJson,
        LineHash          = lineHash,
        LineStatus        = lineStatus,
        StatusReason      = statusReason,
        CreatedAt         = DateTime.UtcNow
    };

    private static FinancialFact BuildFinancialFact(
        ProcessingRun run, StagingRow stagingRow,
        IReadOnlyDictionary<string, string> row,
        string baseType, string competence,
        ClassificationResult cls)
    {
        static decimal Dec(IReadOnlyDictionary<string, string> r, string field)
        {
            if (r.TryGetValue(field, out var v) &&
                decimal.TryParse(v, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return 0m;
        }

        static DateTime? Dt(IReadOnlyDictionary<string, string> r, string field)
            => r.TryGetValue(field, out var v) && DateTime.TryParse(v, out var d) ? d : null;

        // AmountCompetence e AmountCash calculados por tipo de base — spec §11-17
        var (amtComp, amtCash) = baseType.ToUpperInvariant() switch
        {
            "PAG"     => (Dec(row, "TitleAmount"), Dec(row, "PaidAmount")),
            "REC"     => (Dec(row, "TitleAmount"), Dec(row, "ReceivedAmount")),
            "FAT"     => (Dec(row, "TotalAmount"), Dec(row, "TotalAmount")),
            "EMITIDAS"=> (Dec(row, "NetAmount") != 0 ? Dec(row,"NetAmount") : Dec(row,"GrossAmount"),
                          Dec(row, "NetAmount") != 0 ? Dec(row,"NetAmount") : Dec(row,"GrossAmount")),
            "COMP"    => (Dec(row, "TotalAmount"), Dec(row, "TotalAmount")),
            "FOPAG"   => (Dec(row, "PayrollAmount"), Dec(row, "PayrollAmount")),
            "TRANSF"  => (0m, 0m),   // TRANSF não gera receita/despesa por padrão
            _         => (0m, 0m)
        };

        return new FinancialFact
        {
            ProcessingRun_Id    = run.Id,
            SourceStagingRow_Id = stagingRow.Id,
            BaseType            = baseType,
            Competence          = competence,
            ChartOfAccount_Id   = cls.ChartOfAccountId,
            ErpCategory_Id      = cls.ErpCategoryId,
            DocumentNumber      = row.GetValueOrDefault("DocumentNumber"),
            IssueDate           = Dt(row, "IssueDate"),
            DueDate             = Dt(row, "DueDate"),
            PaymentDate         = Dt(row, "PaymentDate"),
            ReceiptDate         = Dt(row, "ReceiptDate"),
            AmountCompetence    = amtComp,
            AmountCash          = amtCash,
            DreGroup            = cls.DreGroup,
            DreSubgroup         = cls.DreSubgroup,
            DreOrder            = cls.DreOrder,
            ClassificationStatus= "Classified",
            CreatedAt           = DateTime.UtcNow
        };
    }
}
