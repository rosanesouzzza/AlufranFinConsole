using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Infrastructure.Persistence;
using AlufranFinConsole.Domain.Entities;
using System.Security.Claims;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// Upload controller for financial files (PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG)
/// Requires JWT authentication
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IFileUploadService _uploadService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UploadController> _logger;

    // Supported file types
    private static readonly string[] ValidFileTypes = { "PAG", "REC", "FAT", "EMITIDAS", "COMP", "TRANSF", "FOPAG" };
    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

    public UploadController(
        IFileUploadService uploadService,
        ApplicationDbContext context,
        ILogger<UploadController> logger)
    {
        _uploadService = uploadService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Upload a financial file with versioning and integrity check
    /// </summary>
    /// <param name="file">File to upload (max 50MB)</param>
    /// <param name="fileType">Type: PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG</param>
    /// <param name="competence">Period (YYYY-MM format)</param>
    /// <returns>Import file metadata with hash</returns>
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string fileType, [FromForm] string competence)
    {
        try
        {
            // Validation
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            if (file.Length > MaxFileSize)
                return BadRequest(new { error = $"File exceeds maximum size of {MaxFileSize / 1024 / 1024} MB" });

            if (string.IsNullOrWhiteSpace(fileType))
                return BadRequest(new { error = "fileType is required" });

            fileType = fileType.ToUpper();
            if (!ValidFileTypes.Contains(fileType))
                return BadRequest(new { error = $"Invalid fileType. Allowed: {string.Join(", ", ValidFileTypes)}" });

            if (string.IsNullOrWhiteSpace(competence))
                return BadRequest(new { error = "competence is required (YYYY-MM format)" });

            if (!System.Text.RegularExpressions.Regex.IsMatch(competence, @"^\d{4}-\d{2}$"))
                return BadRequest(new { error = "competence must be in YYYY-MM format" });

            // Get authenticated user
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "User not authenticated" });

            _logger.LogInformation($"User {userId} uploading file: {file.FileName} ({fileType}/{competence})");

            // Upload file
            using (var stream = file.OpenReadStream())
            {
                // Create import file record
                var importFile = new ImportFile
                {
                    FileName = file.FileName,
                    FileType = fileType,
                    Competence = competence,
                    Status = "PENDING",
                    FileSize = file.Length,
                    StoragePath = $"/var/data/uploads/{fileType}/{competence}/{Guid.NewGuid()}_{file.FileName}",
                    UploadedBy_Id = userId,
                    CreatedAt = DateTime.UtcNow
                };

                // Compute MD5 hash
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    stream.Position = 0;
                    var hash = md5.ComputeHash(stream);
                    importFile.FileHash = Convert.ToHexString(hash);
                }

                // Check for duplicate files
                var existingFile = await _context.ImportFiles
                    .FirstOrDefaultAsync(f => f.FileHash == importFile.FileHash && f.FileType == fileType);

                if (existingFile != null)
                {
                    return BadRequest(new
                    {
                        error = "File already uploaded",
                        existingId = existingFile.Id,
                        message = "This file (same hash) was already uploaded on " + existingFile.CreatedAt.ToShortDateString()
                    });
                }

                // Save file to storage
                var uploadDir = Path.Combine("/var/data/uploads", fileType, competence);
                Directory.CreateDirectory(uploadDir);
                var filePath = Path.Combine(uploadDir, $"{importFile.FileHash}_{file.FileName}");

                stream.Position = 0;
                using (var fileStream = System.IO.File.Create(filePath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                importFile.StoragePath = filePath;

                // Save to database
                _context.ImportFiles.Add(importFile);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"File uploaded successfully: {importFile.Id} ({importFile.FileHash})");

                return CreatedAtAction(nameof(GetFile), new { id = importFile.Id }, new
                {
                    id = importFile.Id,
                    fileName = importFile.FileName,
                    fileHash = importFile.FileHash,
                    fileType = importFile.FileType,
                    competence = importFile.Competence,
                    status = importFile.Status,
                    uploadedAt = importFile.CreatedAt,
                    uploadedBy = userId
                });
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Validation error: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { error = "Failed to upload file", details = ex.Message });
        }
    }

    /// <summary>
    /// Get file metadata by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile(int id)
    {
        var importFile = await _context.ImportFiles.FindAsync(id);
        if (importFile == null)
            return NotFound(new { error = "File not found" });

        return Ok(new
        {
            id = importFile.Id,
            fileName = importFile.FileName,
            fileHash = importFile.FileHash,
            fileType = importFile.FileType,
            competence = importFile.Competence,
            status = importFile.Status,
            uploadedAt = importFile.CreatedAt,
            uploadedBy = importFile.UploadedBy_Id
        });
    }

    /// <summary>
    /// List uploaded files with optional filters
    /// </summary>
    [HttpGet]
    public IActionResult ListFiles(
        [FromQuery] string? fileType = null,
        [FromQuery] string? competence = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.ImportFiles.AsQueryable();

        if (!string.IsNullOrEmpty(fileType))
            query = query.Where(f => f.FileType == fileType.ToUpper());

        if (!string.IsNullOrEmpty(competence))
            query = query.Where(f => f.Competence == competence);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status.ToUpper());

        var files = query
            .OrderByDescending(f => f.CreatedAt)
            .Take(Math.Min(limit, 1000))
            .Select(f => new
            {
                id = f.Id,
                fileName = f.FileName,
                fileHash = f.FileHash,
                fileType = f.FileType,
                competence = f.Competence,
                status = f.Status,
                uploadedAt = f.CreatedAt,
                uploadedBy = f.UploadedBy_Id
            })
            .ToList();

        return Ok(new { total = files.Count, files });
    }

    /// <summary>
    /// Get file statistics
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] string? competence = null)
    {
        var query = _context.ImportFiles.AsQueryable();

        if (!string.IsNullOrEmpty(competence))
            query = query.Where(f => f.Competence == competence);

        var stats = query
            .GroupBy(f => new { f.FileType, f.Competence })
            .Select(g => new
            {
                fileType = g.Key.FileType,
                competence = g.Key.Competence,
                count = g.Count(),
                statuses = g.GroupBy(f => f.Status).Select(s => new { status = s.Key, count = s.Count() })
            })
            .ToList();

        return Ok(stats);
    }
}
