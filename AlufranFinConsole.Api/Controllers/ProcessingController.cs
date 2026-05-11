using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Infrastructure.Persistence;
using AlufranFinConsole.Domain.Entities;
using System.Security.Claims;
using System.Text.Json;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// Fase 5 — Processamento definitivo.
/// Converte registros StagingData VALID/SANITIZED em FinancialTransaction ou PayrollEntry.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessingController : ControllerBase
{
    private const bool Phase8Enabled = false;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcessingController> _logger;

    public ProcessingController(ApplicationDbContext context, ILogger<ProcessingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private IActionResult Phase8Blocked()
    {
        return StatusCode(StatusCodes.Status423Locked, new
        {
            error = "Fase 8 bloqueada por governança.",
            phase = "FASE_8",
            status = "BLOCKED"
        });
    }

    /// <summary>
    /// Processa todos os registros sanitizados de um arquivo de importação.
    /// Cria FinancialTransaction (PAG/REC/FAT/EMITIDAS/COMP/TRANSF) ou PayrollEntry (FOPAG).
    /// Marca os staging records como PROCESSED.
    /// </summary>
    [HttpPost("{importFileId}/process")]
    public async Task<IActionResult> ProcessFile(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        // Somente registros com SanitizedData preenchido podem ser processados
        var stagingRecords = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId
                     && s.ValidationStatus == "VALID"
                     && s.SanitizedData != null && s.SanitizedData != "")
            .ToListAsync();

        if (!stagingRecords.Any())
            return BadRequest(new { error = "Nenhum registro sanitizado disponível para processar. Execute Validar + Sanitizar primeiro." });

        // Verificar se já existem transações para evitar duplicidade
        var alreadyProcessed = await _context.FinancialTransactions
            .AnyAsync(t => t.ImportFile_Id == importFileId);
        var alreadyProcessedFopag = await _context.PayrollEntries
            .AnyAsync(p => p.ImportFile_Id == importFileId);

        if (alreadyProcessed || alreadyProcessedFopag)
            return BadRequest(new
            {
                error = "Este arquivo já foi processado.",
                tip = "Para reprocessar, cancele as transações existentes primeiro via DELETE /api/processing/{importFileId}/cancel."
            });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        var now = DateTime.UtcNow;
        int created = 0, skipped = 0;

        foreach (var staging in stagingRecords)
        {
            try
            {
                var sanitized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staging.SanitizedData)
                                ?? new Dictionary<string, JsonElement>();

                if (importFile.FileType == "FOPAG")
                {
                    var entry = BuildPayrollEntry(importFile, staging, sanitized, userId, now);
                    _context.PayrollEntries.Add(entry);
                }
                else
                {
                    var tx = BuildFinancialTransaction(importFile, staging, sanitized, userId, now);
                    _context.FinancialTransactions.Add(tx);
                }

                staging.ValidationStatus = "PROCESSED";
                staging.ProcessedAt = now;
                staging.UpdatedAt = now;
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao processar staging {staging.Id} (linha {staging.LineNumber}): {ex.Message}");
                skipped++;
            }
        }

        // Atualizar status do ImportFile
        importFile.Status = skipped == 0 ? "COMPLETED" : "COMPLETED_WITH_ERRORS";
        importFile.UpdatedAt = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Processamento concluído: importFileId={importFileId}, criados={created}, ignorados={skipped}");

        return Ok(new
        {
            importFileId,
            fileType = importFile.FileType,
            competence = importFile.Competence,
            created,
            skipped,
            importFileStatus = importFile.Status,
            message = $"Processamento concluído: {created} transações criadas, {skipped} ignoradas."
        });
    }

    /// <summary>
    /// Cancela todas as transações de um arquivo (permite reprocessamento).
    /// Reverte status do staging para VALID e do ImportFile para PROCESSING.
    /// </summary>
    [HttpDelete("{importFileId}/cancel")]
    public async Task<IActionResult> CancelProcessing(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var transactions = await _context.FinancialTransactions
            .Where(t => t.ImportFile_Id == importFileId)
            .ToListAsync();
        var payroll = await _context.PayrollEntries
            .Where(p => p.ImportFile_Id == importFileId)
            .ToListAsync();

        if (!transactions.Any() && !payroll.Any())
            return BadRequest(new { error = "Nenhuma transação encontrada para este arquivo." });

        _context.FinancialTransactions.RemoveRange(transactions);
        _context.PayrollEntries.RemoveRange(payroll);

        // Reverter staging records
        var processed = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId && s.ValidationStatus == "PROCESSED")
            .ToListAsync();
        foreach (var s in processed)
        {
            s.ValidationStatus = "VALID";
            s.ProcessedAt = null;
            s.UpdatedAt = DateTime.UtcNow;
        }

        importFile.Status = "PROCESSING";
        importFile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            importFileId,
            removedTransactions = transactions.Count,
            removedPayroll = payroll.Count,
            revertedStaging = processed.Count,
            message = "Processamento cancelado. Registros revertidos para status VALID."
        });
    }

    /// <summary>
    /// Lista as transações financeiras de uma competência ou arquivo.
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? competence = null,
        [FromQuery] string? fileType = null,
        [FromQuery] int? importFileId = null,
        [FromQuery] int limit = 200)
    {
        var query = _context.FinancialTransactions.AsQueryable();

        if (!string.IsNullOrEmpty(competence))
            query = query.Where(t => t.Competence == competence);
        if (!string.IsNullOrEmpty(fileType))
            query = query.Where(t => t.TransactionType == fileType.ToUpper());
        if (importFileId.HasValue)
            query = query.Where(t => t.ImportFile_Id == importFileId.Value);

        var transactions = await query
            .OrderBy(t => t.Competence).ThenBy(t => t.TransactionType).ThenBy(t => t.DataTransacao)
            .Take(Math.Min(limit, 2000))
            .Select(t => new
            {
                t.Id, t.TransactionType, t.Competence, t.Documento,
                t.Counterpart, t.Valor, t.DataTransacao, t.Descricao,
                t.Categoria, t.StatusTitulo, t.ContaOrigem, t.ContaDestino,
                t.Status, t.ImportFile_Id, t.StagingData_Id, t.CreatedAt
            })
            .ToListAsync();

        var total = transactions.Sum(t => t.Valor);
        return Ok(new { count = transactions.Count, totalValor = total, transactions });
    }

    /// <summary>
    /// Lista os registros de folha de pagamento de uma competência.
    /// </summary>
    [HttpGet("payroll")]
    public async Task<IActionResult> GetPayroll(
        [FromQuery] string? competence = null,
        [FromQuery] int? importFileId = null,
        [FromQuery] int limit = 500)
    {
        var query = _context.PayrollEntries.AsQueryable();

        if (!string.IsNullOrEmpty(competence))
            query = query.Where(p => p.Competence == competence);
        if (importFileId.HasValue)
            query = query.Where(p => p.ImportFile_Id == importFileId.Value);

        var entries = await query
            .OrderBy(p => p.Competence).ThenBy(p => p.Matricula)
            .Take(Math.Min(limit, 5000))
            .Select(p => new
            {
                p.Id, p.Competence, p.Matricula, p.Funcionario, p.Cargo,
                p.ValorBruto, p.Descontos, p.ValorLiquido, p.Status,
                p.ImportFile_Id, p.StagingData_Id, p.CreatedAt
            })
            .ToListAsync();

        var totalBruto  = entries.Sum(p => p.ValorBruto);
        var totalLiquido = entries.Sum(p => p.ValorLiquido);
        return Ok(new { count = entries.Count, totalBruto, totalLiquido, entries });
    }

    /// <summary>
    /// Resumo de fechamento mensal por competência.
    /// Agrupa receitas (REC/FAT), despesas (PAG/COMP), transferências (TRANSF), duplicatas (EMITIDAS) e folha (FOPAG).
    /// </summary>
    [HttpGet("closing-summary")]
    public async Task<IActionResult> GetClosingSummary([FromQuery] string competence)
    {
        if (string.IsNullOrWhiteSpace(competence) || !System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence é obrigatório no formato YYYY-MM" });

        // Transações financeiras — materializar antes de agrupar (SQLite não suporta Sum(decimal) em SQL)
        var txRaw = await _context.FinancialTransactions
            .Where(t => t.Competence == competence && t.Status == "ATIVO")
            .Select(t => new { t.TransactionType, t.Valor })
            .ToListAsync();

        var txList = txRaw
            .GroupBy(t => t.TransactionType)
            .Select(g => new { tipo = g.Key, total = g.Sum(t => t.Valor), count = g.Count() })
            .ToList();

        // Folha de pagamento — materializar também
        var fopagRaw = await _context.PayrollEntries
            .Where(p => p.Competence == competence && p.Status == "ATIVO")
            .Select(p => new { p.ValorBruto, p.ValorLiquido })
            .ToListAsync();

        var fopagResult = fopagRaw.Count == 0 ? null : new
        {
            totalBruto   = fopagRaw.Sum(p => p.ValorBruto),
            totalLiquido = fopagRaw.Sum(p => p.ValorLiquido),
            count        = fopagRaw.Count
        };

        // Consolidar receitas e despesas (em memória, sem SQL aggregation)
        var totalReceitas = txList.Where(t => t.tipo is "REC" or "FAT").Sum(t => t.total);
        var totalDespesas = txList.Where(t => t.tipo is "PAG" or "COMP").Sum(t => t.total);
        var totalFopag    = fopagResult?.totalLiquido ?? 0m;
        var resultado     = totalReceitas - totalDespesas - totalFopag;

        // Linhas de detalhe de despesas (inclui FOPAG se existir)
        var detalhesDespesas = new List<object>();
        foreach (var t in txList.Where(t => t.tipo is "PAG" or "COMP"))
            detalhesDespesas.Add(new { tipo = t.tipo, total = t.total, count = t.count });
        if (fopagResult != null)
            detalhesDespesas.Add(new { tipo = "FOPAG", total = fopagResult.totalLiquido, count = fopagResult.count });

        var summary = new
        {
            competence,
            receitas = new
            {
                total    = totalReceitas,
                detalhes = txList.Where(t => t.tipo is "REC" or "FAT")
                                 .Select(t => new { tipo = t.tipo, total = t.total, count = t.count })
                                 .ToList()
            },
            despesas = new
            {
                total    = totalDespesas + totalFopag,
                detalhes = detalhesDespesas
            },
            transferencias = new
            {
                total = txList.Where(t => t.tipo == "TRANSF").Sum(t => t.total),
                count = txList.Where(t => t.tipo == "TRANSF").Sum(t => t.count)
            },
            duplicatasEmitidas = new
            {
                total = txList.Where(t => t.tipo == "EMITIDAS").Sum(t => t.total),
                count = txList.Where(t => t.tipo == "EMITIDAS").Sum(t => t.count)
            },
            folhaPagamento = new
            {
                totalBruto   = fopagResult?.totalBruto   ?? 0m,
                totalLiquido = fopagResult?.totalLiquido ?? 0m,
                funcionarios = fopagResult?.count        ?? 0
            },
            resultado,
            resultadoLabel = resultado >= 0 ? "SUPERÁVIT" : "DÉFICIT"
        };

        return Ok(summary);
    }

    // ─────────────────────────────────────────────────────────
    // Fase 8 — Aprovação, Snapshot e Status
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna o status de fechamento de uma competência:
    /// arquivos, processamento e aprovação.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string competence)
    {
        if (!Phase8Enabled)
            return Phase8Blocked();

        if (string.IsNullOrWhiteSpace(competence) ||
            !System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence é obrigatório no formato YYYY-MM" });

        var filesRaw = await _context.ImportFiles
            .Where(f => f.Competence == competence)
            .Select(f => new { f.Id, f.FileType, f.FileName, f.Status })
            .ToListAsync();

        var txCount = await _context.FinancialTransactions
            .CountAsync(t => t.Competence == competence && t.Status == "ATIVO");
        var fopagCount = await _context.PayrollEntries
            .CountAsync(p => p.Competence == competence && p.Status == "ATIVO");

        // Aprovação vigente (mais recente com status APROVADO)
        var approval = await _context.ClosingApprovals
            .Where(a => a.Competence == competence && a.Status == "APROVADO")
            .OrderByDescending(a => a.ApprovedAt)
            .Select(a => new { a.Id, a.Status, a.ApprovedBy, a.ApprovedAt, a.Notes })
            .FirstOrDefaultAsync();

        var totalFiles     = filesRaw.Count;
        var completedFiles = filesRaw.Count(f => f.Status is "COMPLETED" or "COMPLETED_WITH_ERRORS");
        var pipelineOk     = totalFiles > 0 && completedFiles == totalFiles;
        var isAprovado     = approval != null;

        return Ok(new
        {
            competence,
            pipeline = new
            {
                totalFiles, completedFiles,
                pipelineOk,
                pendingFiles = totalFiles - completedFiles,
                files = filesRaw
            },
            consolidado = new { txCount, fopagCount, total = txCount + fopagCount },
            aprovacao = isAprovado ? approval : null,
            isAprovado,
            podeAprovar = pipelineOk && !isAprovado,
            statusGeral = isAprovado ? "APROVADO" : (pipelineOk ? "PRONTO_PARA_APROVAÇÃO" : "EM_ANDAMENTO")
        });
    }

    /// <summary>
    /// Aprova formalmente o fechamento de uma competência.
    /// Gera snapshot da DRE no momento da aprovação.
    /// </summary>
    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromQuery] string competence, [FromBody] ApproveRequest req)
    {
        if (!Phase8Enabled)
            return Phase8Blocked();

        if (string.IsNullOrWhiteSpace(competence) ||
            !System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence é obrigatório no formato YYYY-MM" });

        // Verificar se já está aprovado
        var existing = await _context.ClosingApprovals
            .AnyAsync(a => a.Competence == competence && a.Status == "APROVADO");
        if (existing)
            return Conflict(new { error = "Esta competência já está aprovada. Para reabrir, use DELETE /api/processing/approve." });

        // Verificar pipeline completo
        var files = await _context.ImportFiles
            .Where(f => f.Competence == competence).ToListAsync();
        if (!files.Any())
            return BadRequest(new { error = "Nenhum arquivo encontrado para esta competência." });

        var pendentes = files.Count(f => f.Status is "PENDING" or "PROCESSING");
        if (pendentes > 0)
            return BadRequest(new { error = $"Pipeline incompleto: {pendentes} arquivo(s) ainda não processados." });

        // Gerar snapshot da DRE (reutiliza a mesma lógica do endpoint GET /dre)
        var dreSnapshot = await BuildDreSnapshotJson(competence);

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? "sistema";
        var now = DateTime.UtcNow;

        var approval = new ClosingApproval
        {
            Competence  = competence,
            Status      = "APROVADO",
            ApprovedBy  = userId,
            ApprovedAt  = now,
            Notes       = req?.Notes,
            DreSnapshot = dreSnapshot,
            CreatedAt   = now
        };

        _context.ClosingApprovals.Add(approval);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Fechamento aprovado: competence={Comp} by={User}", competence, userId);

        return Ok(new
        {
            id          = approval.Id,
            competence,
            status      = "APROVADO",
            approvedBy  = userId,
            approvedAt  = now,
            notes       = req?.Notes,
            message     = $"Fechamento de {competence} aprovado com sucesso. Snapshot da DRE registrado."
        });
    }

    /// <summary>
    /// Reabre um fechamento aprovado (registra histórico).
    /// </summary>
    [HttpDelete("approve")]
    public async Task<IActionResult> Reopen([FromQuery] string competence, [FromBody] ApproveRequest? req)
    {
        if (!Phase8Enabled)
            return Phase8Blocked();

        if (string.IsNullOrWhiteSpace(competence) ||
            !System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence é obrigatório no formato YYYY-MM" });

        var approval = await _context.ClosingApprovals
            .Where(a => a.Competence == competence && a.Status == "APROVADO")
            .OrderByDescending(a => a.ApprovedAt)
            .FirstOrDefaultAsync();

        if (approval == null)
            return NotFound(new { error = "Nenhuma aprovação vigente encontrada para esta competência." });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? "sistema";

        approval.Status    = "REABERTO";
        approval.Notes     = req?.Notes != null
            ? $"[REABERTO por {userId}] {req.Notes}\n[APROVAÇÃO ORIGINAL] {approval.Notes}"
            : $"[REABERTO por {userId} em {DateTime.UtcNow:dd/MM/yyyy HH:mm}]\n[APROVAÇÃO ORIGINAL] {approval.Notes}";
        approval.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Fechamento reaberto: competence={Comp} by={User}", competence, userId);

        return Ok(new
        {
            competence,
            status    = "REABERTO",
            reopenedBy = userId,
            message   = $"Fechamento de {competence} reaberto. Pode ser editado e reaprovado."
        });
    }

    // ── Snapshot helper ───────────────────────────────────────────────────

    private async Task<string> BuildDreSnapshotJson(string competence)
    {
        var txRaw = await _context.FinancialTransactions
            .Where(t => t.Competence == competence && t.Status == "ATIVO")
            .Select(t => new { t.TransactionType, t.Valor, t.StatusTitulo })
            .ToListAsync();

        var fopagRaw = await _context.PayrollEntries
            .Where(p => p.Competence == competence && p.Status == "ATIVO")
            .Select(p => new { p.ValorBruto, p.ValorLiquido })
            .ToListAsync();

        decimal Soma(string tipo) => txRaw.Where(t => t.TransactionType == tipo).Sum(t => t.Valor);

        var receitaBruta  = Soma("REC") + Soma("FAT");
        var deducoes      = txRaw.Where(t => t.TransactionType == "EMITIDAS" && t.StatusTitulo == "CANCELADO").Sum(t => t.Valor);
        var csp           = Soma("COMP");
        var despesasOp    = Soma("PAG");
        var fopagLiq      = fopagRaw.Sum(p => p.ValorLiquido);
        var lucroBruto    = receitaBruta - deducoes - csp;
        var resultadoOp   = lucroBruto - despesasOp - fopagLiq;

        var snapshot = new
        {
            competence,
            snapshotAt     = DateTime.UtcNow,
            receitaBruta, deducoes,
            receitaLiquida = receitaBruta - deducoes,
            csp, lucroBruto,
            despesasOp, fopagLiq,
            totalDespesas  = despesasOp + fopagLiq + csp,
            resultadoOp,
            resultadoLiquido = resultadoOp,
            label          = resultadoOp >= 0 ? "SUPERÁVIT" : "DÉFICIT",
            funcionarios   = fopagRaw.Count
        };

        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }

    /// <summary>
    /// Demonstrativo de Resultado do Exercício (DRE) por competência.
    /// Estrutura: Receita Bruta → Deduções → Receita Líquida → CSP → Lucro Bruto
    ///            → Despesas Operacionais → Despesas com Pessoal → Resultado Operacional
    ///            → Resultado Líquido (+ transferências informativas)
    /// </summary>
    [HttpGet("dre")]
    public async Task<IActionResult> GetDre([FromQuery] string competence)
    {
        if (string.IsNullOrWhiteSpace(competence) ||
            !System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence é obrigatório no formato YYYY-MM" });

        // ── Materializar tudo em memória (SQLite não agrega decimal em SQL) ──
        var txRaw = await _context.FinancialTransactions
            .Where(t => t.Competence == competence && t.Status == "ATIVO")
            .Select(t => new { t.TransactionType, t.Valor, t.StatusTitulo, t.Counterpart,
                               t.Documento, t.DataTransacao, t.Descricao, t.Categoria })
            .ToListAsync();

        var fopagRaw = await _context.PayrollEntries
            .Where(p => p.Competence == competence && p.Status == "ATIVO")
            .Select(p => new { p.ValorBruto, p.ValorLiquido, p.Funcionario, p.Cargo,
                               p.Matricula, p.Descontos })
            .ToListAsync();

        // ── Agrupamentos base ───────────────────────────────────────────────
        decimal Soma(string tipo) =>
            txRaw.Where(t => t.TransactionType == tipo).Sum(t => t.Valor);
        int Qtd(string tipo) =>
            txRaw.Count(t => t.TransactionType == tipo);

        var recTotal    = Soma("REC");
        var fatTotal    = Soma("FAT");
        var emitTotal   = txRaw.Where(t => t.TransactionType == "EMITIDAS"
                                        && t.StatusTitulo == "CANCELADO").Sum(t => t.Valor);
        var compTotal   = Soma("COMP");
        var pagTotal    = Soma("PAG");
        var transfTotal = Soma("TRANSF");
        var fopagBruto  = fopagRaw.Sum(p => p.ValorBruto);
        var fopagLiq    = fopagRaw.Sum(p => p.ValorLiquido);

        // ── DRE calculada ───────────────────────────────────────────────────
        var receitaBruta       = recTotal + fatTotal;
        var deducoes           = emitTotal;                      // NF canceladas
        var receitaLiquida     = receitaBruta - deducoes;
        var csp                = compTotal;                      // Custo dos Serviços
        var lucroBruto         = receitaLiquida - csp;
        var despesasOp         = pagTotal;
        var despesasPessoal    = fopagLiq;
        var resultadoOp        = lucroBruto - despesasOp - despesasPessoal;
        var resultadoLiquido   = resultadoOp;                    // sem rec. financeiras por ora

        decimal Margem(decimal valor) =>
            receitaBruta > 0 ? Math.Round(valor / receitaBruta * 100, 2) : 0m;

        // ── Detalhe de transações por tipo ──────────────────────────────────
        static object Detalhe(IEnumerable<dynamic> list) => list
            .Select(t => new { t.Documento, t.Counterpart, t.Valor, t.DataTransacao,
                                t.Descricao, t.Categoria })
            .OrderByDescending(t => t.Valor)
            .Take(100)
            .ToList();

        var dre = new
        {
            competence,
            geradoEm = DateTime.UtcNow,

            // ── Linha 1: Receita Bruta ──
            receitaBruta = new
            {
                valor    = receitaBruta,
                margem   = 100m,
                itens = new[]
                {
                    new { codigo = "1.1", descricao = "Receitas de Serviços (REC)",
                          valor = recTotal,  quantidade = Qtd("REC") },
                    new { codigo = "1.2", descricao = "Faturamento Emitido (FAT)",
                          valor = fatTotal,  quantidade = Qtd("FAT") }
                }
            },

            // ── Linha 2: Deduções ──
            deducoes = new
            {
                valor    = deducoes,
                margem   = Margem(deducoes),
                itens = new[]
                {
                    new { codigo = "2.1", descricao = "NF Canceladas (EMITIDAS/CANCELADO)",
                          valor = emitTotal, quantidade = txRaw.Count(t => t.TransactionType == "EMITIDAS" && t.StatusTitulo == "CANCELADO") }
                }
            },

            // ── Linha 3: Receita Líquida ──
            receitaLiquida = new { valor = receitaLiquida, margem = Margem(receitaLiquida) },

            // ── Linha 4: Custo dos Serviços Prestados ──
            custoServicos = new
            {
                valor    = csp,
                margem   = Margem(csp),
                itens = new[]
                {
                    new { codigo = "4.1", descricao = "Compras / NF de Entrada (COMP)",
                          valor = compTotal, quantidade = Qtd("COMP") }
                }
            },

            // ── Linha 5: Lucro Bruto ──
            lucroBruto = new { valor = lucroBruto, margem = Margem(lucroBruto) },

            // ── Linha 6: Despesas Operacionais ──
            despesasOperacionais = new
            {
                valor    = despesasOp,
                margem   = Margem(despesasOp),
                itens = new[]
                {
                    new { codigo = "6.1", descricao = "Contas a Pagar (PAG)",
                          valor = pagTotal, quantidade = Qtd("PAG") }
                }
            },

            // ── Linha 7: Despesas com Pessoal ──
            despesasPessoal = new
            {
                valorLiquido = despesasPessoal,
                valorBruto   = fopagBruto,
                margem       = Margem(despesasPessoal),
                funcionarios = fopagRaw.Count,
                itens = new[]
                {
                    new { codigo = "7.1", descricao = "Folha de Pagamento - Líquido (FOPAG)",
                          valor = fopagLiq, valorBruto = fopagBruto,
                          quantidade = fopagRaw.Count }
                },
                detalhe = fopagRaw.Select(p => new
                    { p.Matricula, p.Funcionario, p.Cargo,
                      p.ValorBruto, p.Descontos, p.ValorLiquido })
                    .OrderBy(p => p.Funcionario)
                    .ToList()
            },

            // ── Linha 8: Resultado Operacional ──
            resultadoOperacional = new
            {
                valor  = resultadoOp,
                margem = Margem(resultadoOp),
                label  = resultadoOp >= 0 ? "SUPERÁVIT OPERACIONAL" : "DÉFICIT OPERACIONAL"
            },

            // ── Informativo: Transferências (neutro na DRE) ──
            transferencias = new
            {
                valor      = transfTotal,
                quantidade = Qtd("TRANSF"),
                nota       = "Informativo — movimentação entre contas, sem impacto no resultado"
            },

            // ── Linha 9: Resultado Líquido ──
            resultadoLiquido = new
            {
                valor  = resultadoLiquido,
                margem = Margem(resultadoLiquido),
                label  = resultadoLiquido >= 0 ? "SUPERÁVIT" : "DÉFICIT"
            }
        };

        return Ok(dre);
    }

    // ──────────────────────────────────────────────
    // Helpers privados
    // ──────────────────────────────────────────────

    private static FinancialTransaction BuildFinancialTransaction(
        ImportFile importFile,
        StagingData staging,
        Dictionary<string, JsonElement> data,
        string userId,
        DateTime now)
    {
        string Get(string key) =>
            data.TryGetValue(key, out var el) ? el.ToString() : "";

        decimal GetDecimal(string key) =>
            data.TryGetValue(key, out var el) && el.TryGetDecimal(out var d) ? d : 0m;

        DateTime GetDate(string key) =>
            data.TryGetValue(key, out var el) && DateTime.TryParse(el.ToString(), out var dt) ? dt : now;

        var tx = new FinancialTransaction
        {
            ImportFile_Id   = importFile.Id,
            StagingData_Id  = staging.Id,
            TransactionType = importFile.FileType,
            Competence      = importFile.Competence,
            Status          = "ATIVO",
            ProcessedBy_Id  = userId,
            CreatedAt       = now
        };

        switch (importFile.FileType)
        {
            case "PAG":
                tx.Documento    = Get("documento");
                tx.Counterpart  = Get("fornecedor_normalizado");
                tx.CounterpartKey = Get("fornecedor_normalizado");
                tx.Valor        = GetDecimal("valor");
                tx.DataTransacao = GetDate("data_pagamento");
                tx.Descricao    = staging.RawData;
                break;

            case "REC":
                tx.Documento    = Get("documento");
                tx.Counterpart  = Get("cliente_normalizado");
                tx.CounterpartKey = Get("cliente_normalizado");
                tx.Valor        = GetDecimal("valor");
                tx.DataTransacao = GetDate("data_recebimento");
                tx.Descricao    = staging.RawData;
                break;

            case "FAT":
                tx.Documento    = Get("numero_nf");
                tx.Counterpart  = Get("cliente_normalizado");
                tx.CounterpartKey = Get("cliente_normalizado");
                tx.Valor        = GetDecimal("valor");
                tx.DataTransacao = GetDate("data_emissao");
                tx.Descricao    = Get("descricao");
                break;

            case "EMITIDAS":
                tx.Documento      = Get("numero_doc");
                tx.Counterpart    = Get("cliente_normalizado");
                tx.CounterpartKey = Get("cliente_normalizado");
                tx.Valor          = GetDecimal("valor");
                tx.DataTransacao  = GetDate("data_emissao");
                tx.DataVencimento = GetDate("data_vencimento");
                tx.StatusTitulo   = Get("status");
                tx.Descricao      = staging.RawData;
                break;

            case "COMP":
                tx.Documento    = Get("numero_nf");
                tx.Counterpart  = Get("fornecedor_normalizado");
                tx.CounterpartKey = Get("fornecedor_normalizado");
                tx.Valor        = GetDecimal("valor");
                tx.DataTransacao = GetDate("data_entrada");
                tx.Categoria    = Get("categoria");
                tx.Descricao    = Get("descricao");
                break;

            case "TRANSF":
                tx.Documento    = Get("numero_doc");
                tx.ContaOrigem  = Get("conta_origem");
                tx.ContaDestino = Get("conta_destino");
                tx.Counterpart  = $"{Get("conta_origem")} → {Get("conta_destino")}";
                tx.CounterpartKey = $"{Get("conta_origem")}_{Get("conta_destino")}";
                tx.Valor        = GetDecimal("valor");
                tx.DataTransacao = GetDate("data");
                tx.Descricao    = Get("descricao");
                break;
        }

        return tx;
    }

    // ── DTO interno ──────────────────────────────────────────────────────
    public class ApproveRequest
    {
        public string? Notes { get; set; }
    }

    private static PayrollEntry BuildPayrollEntry(
        ImportFile importFile,
        StagingData staging,
        Dictionary<string, JsonElement> data,
        string userId,
        DateTime now)
    {
        string Get(string key) =>
            data.TryGetValue(key, out var el) ? el.ToString() : "";

        decimal GetDecimal(string key) =>
            data.TryGetValue(key, out var el) && el.TryGetDecimal(out var d) ? d : 0m;

        return new PayrollEntry
        {
            ImportFile_Id   = importFile.Id,
            StagingData_Id  = staging.Id,
            Competence      = data.TryGetValue("competencia", out var comp) && !string.IsNullOrEmpty(comp.ToString())
                                  ? comp.ToString()
                                  : importFile.Competence,
            Matricula       = Get("matricula"),
            Funcionario     = staging.RawData.Split(',').Length > 1
                                  ? staging.RawData.Split(',')[1].Trim()
                                  : Get("funcionario_normalizado"),
            FuncionarioKey  = Get("funcionario_normalizado"),
            Cargo           = Get("cargo"),
            ValorBruto      = GetDecimal("valor_bruto"),
            Descontos       = GetDecimal("descontos"),
            ValorLiquido    = GetDecimal("valor_liquido"),
            Status          = "ATIVO",
            ProcessedBy_Id  = userId,
            CreatedAt       = now
        };
    }
}
