using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Domain.Entities;
using System.Security.Claims;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// POST /api/import-files         — registra um ImportFile
/// POST /api/import-files/{id}/versions — cria nova FileVersion (re-importação)
/// </summary>
[ApiController]
[Route("api/import-files")]
[Authorize]
public class ImportFilesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ImportFilesController> _logger;
    private readonly string _uploadBasePath;

    public ImportFilesController(
        IApplicationDbContext context,
        ILogger<ImportFilesController> logger,
        IConfiguration config)
    {
        _context = context;
        _logger  = logger;
        var path = config["Storage:UploadPath"] ?? "uploads";
        _uploadBasePath = Path.IsPathRooted(path) ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    // POST /api/import-files
    [HttpPost]
    public async Task<IActionResult> CreateImportFile(
        IFormFile file,
        [FromForm] string fileType,
        [FromForm] string competence,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Arquivo não fornecido" });

        fileType = fileType?.ToUpperInvariant() ?? "";
        if (!new[] { "PAG","REC","FAT","EMITIDAS","COMP","TRANSF","FOPAG" }.Contains(fileType))
            return BadRequest(new { error = "fileType inválido" });

        if (!System.Text.RegularExpressions.Regex.IsMatch(competence ?? "", @"^\d{4}-\d{2}$"))
            return BadRequest(new { error = "competence deve ser YYYY-MM" });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        // Calcular hash
        string hash;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray()));
        }

        // Verificar duplicidade
        var dup = await _context.ImportFiles
            .FirstOrDefaultAsync(f => f.FileHash == hash && f.FileType == fileType, ct);
        if (dup != null)
            return Conflict(new { error = "Arquivo idêntico já importado", existingId = dup.Id });

        // Salvar arquivo
        var dir = Path.Combine(_uploadBasePath, fileType, competence);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{hash}_{file.FileName}");
        using (var fs = System.IO.File.Create(filePath))
        {
            file.OpenReadStream().CopyTo(fs);
        }

        var importFile = new ImportFile
        {
            FileName      = file.FileName,
            FileHash      = hash,
            FileSize      = file.Length,
            FileType      = fileType,
            Competence    = competence,
            Status        = "PENDING",
            StoragePath   = filePath,
            UploadedBy_Id = userId,
            UploadedAt    = DateTime.UtcNow,
            CreatedAt     = DateTime.UtcNow
        };
        _context.ImportFiles.Add(importFile);
        await _context.SaveChangesAsync(ct);

        // Criar FileVersion 1
        var version = new FileVersion
        {
            ImportFile_Id  = importFile.Id,
            VersionNumber  = 1,
            FileHash       = hash,
            FileSizeBytes  = file.Length,
            StoragePath    = filePath,
            Status         = "ACTIVE",
            CreatedBy_Id   = userId,
            CreatedAt      = DateTime.UtcNow
        };
        _context.FileVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ImportFile {Id} criado por {User}", importFile.Id, userId);

        return CreatedAtAction(nameof(GetImportFile), new { id = importFile.Id }, new
        {
            id            = importFile.Id,
            fileVersionId = version.Id,
            fileName      = importFile.FileName,
            fileType      = importFile.FileType,
            competence    = importFile.Competence,
            fileHash      = importFile.FileHash,
            status        = importFile.Status
        });
    }

    // POST /api/import-files/{id}/versions
    [HttpPost("{id}/versions")]
    public async Task<IActionResult> CreateVersion(
        int id,
        IFormFile file,
        [FromForm] string? notes,
        CancellationToken ct)
    {
        var importFile = await _context.ImportFiles.FindAsync([id], ct);
        if (importFile == null) return NotFound(new { error = "ImportFile não encontrado" });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        // Calcular hash da nova versão
        string hash;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray()));
        }

        // Marcar versões anteriores como supersedidas
        var existing = await _context.FileVersions
            .Where(v => v.ImportFile_Id == id && v.Status == "ACTIVE")
            .ToListAsync(ct);
        foreach (var v in existing) v.Status = "SUPERSEDED";

        var nextVersion = existing.Count + 1;

        var dir = Path.Combine(_uploadBasePath, importFile.FileType, importFile.Competence, "versions");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"v{nextVersion}_{hash}_{file.FileName}");
        using (var fs = System.IO.File.Create(filePath))
            file.OpenReadStream().CopyTo(fs);

        var version = new FileVersion
        {
            ImportFile_Id = id,
            VersionNumber = nextVersion,
            FileHash      = hash,
            FileSizeBytes = file.Length,
            StoragePath   = filePath,
            Status        = "ACTIVE",
            Notes         = notes,
            CreatedBy_Id  = userId,
            CreatedAt     = DateTime.UtcNow
        };
        _context.FileVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("FileVersion {VerId} (v{Num}) criada para ImportFile {Id}",
            version.Id, nextVersion, id);

        return CreatedAtAction(nameof(GetVersion), new { id, versionId = version.Id }, new
        {
            fileVersionId = version.Id,
            importFileId  = id,
            versionNumber = nextVersion,
            fileHash      = hash,
            notes
        });
    }

    // GET /api/import-files/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetImportFile(int id, CancellationToken ct)
    {
        var f = await _context.ImportFiles.FindAsync([id], ct);
        if (f == null) return NotFound();
        return Ok(new { f.Id, f.FileName, f.FileType, f.Competence, f.Status, f.FileHash, f.CreatedAt });
    }

    // GET /api/import-files/{id}/versions/{versionId}
    [HttpGet("{id}/versions/{versionId}")]
    public async Task<IActionResult> GetVersion(int id, int versionId, CancellationToken ct)
    {
        var v = await _context.FileVersions
            .FirstOrDefaultAsync(x => x.Id == versionId && x.ImportFile_Id == id, ct);
        if (v == null) return NotFound();
        return Ok(new { v.Id, v.ImportFile_Id, v.VersionNumber, v.FileHash, v.Status, v.CreatedAt });
    }
}
