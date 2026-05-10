using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Infrastructure.Persistence;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// Controller for import file management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImportController> _logger;

    public ImportController(ApplicationDbContext context, ILogger<ImportController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// List all import files
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListImportFiles([FromQuery] int limit = 50)
    {
        try
        {
            var files = await _context.ImportFiles
                .OrderByDescending(f => f.CreatedAt)
                .Take(Math.Min(limit, 100))
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FileType,
                    f.Status,
                    f.Competence,
                    f.CreatedAt
                })
                .ToListAsync();

            return Ok(new { files });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error listing import files: {ex.Message}");
            return StatusCode(500, new { error = "Failed to list import files", detail = ex.Message });
        }
    }

    /// <summary>
    /// Get import file details
    /// </summary>
    [HttpGet("{importFileId}")]
    public async Task<IActionResult> GetImportFile(int importFileId)
    {
        try
        {
            var file = await _context.ImportFiles.FindAsync(importFileId);
            if (file == null)
                return NotFound(new { error = "Import file not found" });

            var stagingCount = await _context.StagingData
                .Where(s => s.ImportFile_Id == importFileId)
                .CountAsync();

            return Ok(new
            {
                file.Id,
                file.FileName,
                file.FileType,
                file.Status,
                file.Competence,
                file.CreatedAt,
                stagingRecordCount = stagingCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting import file: {ex.Message}");
            return StatusCode(500, new { error = "Failed to get import file", detail = ex.Message });
        }
    }
}
