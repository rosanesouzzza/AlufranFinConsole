using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Application.Services;
using System.Security.Claims;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// POST /api/processing-runs/{fileVersionId}/start  — inicia o pipeline de saneamento
/// GET  /api/processing-runs/{id}                   — status do run
/// GET  /api/processing-runs/{id}/staging           — linhas staged
/// GET  /api/processing-runs/{id}/discarded         — linhas descartadas
/// GET  /api/processing-runs/{id}/qa                — issues de QA
/// </summary>
[ApiController]
[Route("api/processing-runs")]
[Authorize]
public class ProcessingRunsController : ControllerBase
{
    private readonly IFinancialSanitizationService _sanitization;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProcessingRunsController> _logger;

    public ProcessingRunsController(
        IFinancialSanitizationService sanitization,
        IApplicationDbContext context,
        ILogger<ProcessingRunsController> logger)
    {
        _sanitization = sanitization;
        _context      = context;
        _logger       = logger;
    }

    // POST /api/processing-runs/{fileVersionId}/start
    [HttpPost("{fileVersionId}/start")]
    public async Task<IActionResult> StartRun(int fileVersionId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        var version = await _context.FileVersions.FindAsync([fileVersionId], ct);
        if (version == null) return NotFound(new { error = "FileVersion não encontrada" });

        _logger.LogInformation("StartRun solicitado para FileVersion {Id} por {User}", fileVersionId, userId);

        var result = await _sanitization.RunAsync(fileVersionId, userId, ct);

        return Ok(new
        {
            processingRunId = result.ProcessingRunId,
            status          = result.Status,
            totalRows       = result.TotalRows,
            validRows       = result.ValidRows,
            discardedRows   = result.DiscardedRows,
            qaRows          = result.QaRows,
            factsGenerated  = result.FactsGenerated,
            hasBlockingQa   = result.HasBlockingQa,
            approvalBlocked = result.HasBlockingQa
        });
    }

    // GET /api/processing-runs/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRun(int id, CancellationToken ct)
    {
        var run = await _context.ProcessingRuns
            .Include(r => r.FileVersion)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (run == null) return NotFound();

        return Ok(new
        {
            run.Id, run.BaseType, run.Competence, run.Status,
            run.TotalRows, run.ValidRows, run.DiscardedRows, run.QaRows, run.FactsGenerated,
            run.StartedAt, run.CompletedAt, run.ErrorMessage,
            fileVersion = new { run.FileVersion.Id, run.FileVersion.VersionNumber, run.FileVersion.FileHash }
        });
    }

    // GET /api/processing-runs/{id}/staging
    [HttpGet("{id}/staging")]
    public async Task<IActionResult> GetStagingRows(
        int id,
        [FromQuery] string? lineStatus,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var query = _context.StagingRows.Where(r => r.ProcessingRun_Id == id);
        if (!string.IsNullOrEmpty(lineStatus))
            query = query.Where(r => r.LineStatus == lineStatus.ToUpperInvariant());

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(r => r.OriginalRowNumber)
            .Skip(skip).Take(Math.Min(take, 500))
            .Select(r => new
            {
                r.Id, r.OriginalRowNumber, r.LineStatus, r.StatusReason,
                r.LineHash, r.NormalizedJson, r.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, skip, take = rows.Count, rows });
    }

    // GET /api/processing-runs/{id}/discarded
    [HttpGet("{id}/discarded")]
    public async Task<IActionResult> GetDiscardedRows(
        int id,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var total = await _context.DiscardedRows.CountAsync(d => d.ProcessingRun_Id == id, ct);
        var rows = await _context.DiscardedRows
            .Where(d => d.ProcessingRun_Id == id)
            .OrderBy(d => d.OriginalRowNumber)
            .Skip(skip).Take(Math.Min(take, 500))
            .Select(d => new
            {
                d.Id, d.OriginalRowNumber, d.DiscardReason, d.DiscardDetail, d.RawJson, d.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, skip, take = rows.Count, rows });
    }

    // GET /api/processing-runs/{id}/qa
    [HttpGet("{id}/qa")]
    public async Task<IActionResult> GetQaIssues(
        int id,
        [FromQuery] string? severity,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var query = _context.QaIssues.Where(q => q.ProcessingRun_Id == id);
        if (!string.IsNullOrEmpty(severity))
            query = query.Where(q => q.Severity == severity);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.Status == status);

        var total    = await query.CountAsync(ct);
        var blocking = await query.CountAsync(q => q.Severity == "Blocking", ct);
        var issues   = await query
            .OrderBy(q => q.Severity)
            .ThenBy(q => q.OriginalRowNumber)
            .Skip(skip).Take(Math.Min(take, 500))
            .Select(q => new
            {
                q.Id, q.OriginalRowNumber, q.IssueType, q.Severity, q.Message, q.Status, q.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, blocking, approvalBlocked = blocking > 0, skip, take = issues.Count, issues });
    }
}
