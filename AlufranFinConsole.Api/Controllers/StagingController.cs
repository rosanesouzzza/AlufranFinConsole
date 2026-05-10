using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Infrastructure.Persistence;
using AlufranFinConsole.Domain.Entities;
using System.Text.Json;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// Staging controller for data validation and sanitization workflow
/// Phase 4: Data staging and cleanup before processing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StagingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDataValidationService _validationService;
    private readonly ILogger<StagingController> _logger;

    public StagingController(
        ApplicationDbContext context,
        IDataValidationService validationService,
        ILogger<StagingController> logger)
    {
        _context = context;
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// List staging records for an import file
    /// </summary>
    [HttpGet("{importFileId}")]
    public async Task<IActionResult> ListStagingRecords(int importFileId, [FromQuery] string? status = null, [FromQuery] int limit = 100)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var query = _context.StagingData.Where(s => s.ImportFile_Id == importFileId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.ValidationStatus == status.ToUpper());

        var records = await query
            .OrderBy(s => s.LineNumber)
            .Take(Math.Min(limit, 1000))
            .Select(s => new
            {
                s.Id,
                s.LineNumber,
                s.RawData,
                s.ParsedData,
                s.ValidationStatus,
                s.ValidationErrors,
                s.SanitizedData,
                s.ProcessedAt,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total = records.Count, records });
    }

    /// <summary>
    /// Get summary statistics for staging records
    /// </summary>
    [HttpGet("{importFileId}/summary")]
    public async Task<IActionResult> GetStagingSummary(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var records = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId)
            .ToListAsync();

        var summary = new
        {
            importFileId,
            fileName = importFile.FileName,
            totalLines = records.Count,
            pending = records.Count(s => s.ValidationStatus == "PENDING"),
            valid = records.Count(s => s.ValidationStatus == "VALID"),
            invalid = records.Count(s => s.ValidationStatus == "INVALID"),
            duplicate = records.Count(s => s.ValidationStatus == "DUPLICATE"),
            processed = records.Count(s => s.ValidationStatus == "PROCESSED"),
            percentValid = records.Count > 0 ? (double)records.Count(s => s.ValidationStatus == "VALID") / records.Count * 100 : 0
        };

        return Ok(summary);
    }

    /// <summary>
    /// Validate all pending records for an import file
    /// Processes: RawData → ParsedData (validation)
    /// </summary>
    [HttpPost("{importFileId}/validate")]
    public async Task<IActionResult> ValidateStagingRecords(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var pendingRecords = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId && s.ValidationStatus == "PENDING")
            .ToListAsync();

        _logger.LogInformation($"Validating {pendingRecords.Count} pending records from file {importFileId}");

        int validated = 0, failed = 0;

        foreach (var record in pendingRecords)
        {
            try
            {
                // Parse
                var parseResult = _validationService.ParseLine(importFile.FileType, record.RawData);
                if (!parseResult.Success)
                {
                    record.ValidationStatus = "INVALID";
                    record.ValidationErrors = JsonSerializer.Serialize(new[] { parseResult.Error });
                    failed++;
                    continue;
                }

                // Validate
                var validateResult = _validationService.ValidateParsedData(importFile.FileType, parseResult.ParsedData);
                record.ParsedData = JsonSerializer.Serialize(parseResult.ParsedData);

                if (!validateResult.Success)
                {
                    record.ValidationStatus = "INVALID";
                    record.ValidationErrors = JsonSerializer.Serialize(validateResult.Errors);
                    failed++;
                }
                else
                {
                    record.ValidationStatus = "VALID";
                    validated++;
                }

                record.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating line {record.LineNumber}: {ex.Message}");
                record.ValidationStatus = "INVALID";
                record.ValidationErrors = JsonSerializer.Serialize(new[] { $"Validation error: {ex.Message}" });
                failed++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            importFileId,
            processed = validated + failed,
            validated,
            failed,
            message = $"Validated {validated + failed} records ({validated} valid, {failed} invalid)"
        });
    }

    /// <summary>
    /// Sanitize valid records
    /// Processes: ParsedData → SanitizedData (normalization & transformation)
    /// </summary>
    [HttpPost("{importFileId}/sanitize")]
    public async Task<IActionResult> SanitizeStagingRecords(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var validRecords = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId && s.ValidationStatus == "VALID")
            .ToListAsync();

        _logger.LogInformation($"Sanitizing {validRecords.Count} valid records from file {importFileId}");

        int sanitized = 0, failed = 0;

        foreach (var record in validRecords)
        {
            try
            {
                var parsedData = JsonSerializer.Deserialize<Dictionary<string, string>>(record.ParsedData) ?? new();
                var sanitizeResult = _validationService.SanitizeData(importFile.FileType, parsedData);

                if (!sanitizeResult.Success)
                {
                    failed++;
                    record.ValidationErrors = JsonSerializer.Serialize(new[] { sanitizeResult.Error });
                    continue;
                }

                record.SanitizedData = JsonSerializer.Serialize(sanitizeResult.SanitizedData);
                record.UpdatedAt = DateTime.UtcNow;
                sanitized++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sanitizing line {record.LineNumber}: {ex.Message}");
                record.ValidationErrors = JsonSerializer.Serialize(new[] { $"Sanitize error: {ex.Message}" });
                failed++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            importFileId,
            processed = sanitized + failed,
            sanitized,
            failed,
            message = $"Sanitized {sanitized + failed} records ({sanitized} success, {failed} failed)"
        });
    }

    /// <summary>
    /// Get detailed validation report for staging records
    /// </summary>
    [HttpGet("{importFileId}/report")]
    public async Task<IActionResult> GetValidationReport(int importFileId)
    {
        var importFile = await _context.ImportFiles.FindAsync(importFileId);
        if (importFile == null)
            return NotFound(new { error = "Import file not found" });

        var records = await _context.StagingData
            .Where(s => s.ImportFile_Id == importFileId)
            .OrderBy(s => s.LineNumber)
            .ToListAsync();

        var invalidRecords = records
            .Where(s => s.ValidationStatus == "INVALID")
            .Select(s => new
            {
                s.LineNumber,
                s.RawData,
                errors = string.IsNullOrEmpty(s.ValidationErrors)
                    ? new string[0]
                    : JsonSerializer.Deserialize<string[]>(s.ValidationErrors) ?? new string[0]
            })
            .ToList();

        var report = new
        {
            importFileId,
            fileName = importFile.FileName,
            fileType = importFile.FileType,
            generatedAt = DateTime.UtcNow,
            summary = new
            {
                total = records.Count,
                pending = records.Count(s => s.ValidationStatus == "PENDING"),
                valid = records.Count(s => s.ValidationStatus == "VALID"),
                invalid = records.Count(s => s.ValidationStatus == "INVALID"),
                duplicate = records.Count(s => s.ValidationStatus == "DUPLICATE"),
                processed = records.Count(s => s.ValidationStatus == "PROCESSED")
            },
            invalidRecords
        };

        return Ok(report);
    }
}
