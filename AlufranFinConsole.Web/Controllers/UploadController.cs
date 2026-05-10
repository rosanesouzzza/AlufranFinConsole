using Microsoft.AspNetCore.Mvc;
using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Infrastructure.Persistence;

namespace AlufranFinConsole.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IFileUploadService _uploadService;
    private readonly ApplicationDbContext _context;
    private readonly ISession _session;

    public UploadController(IFileUploadService uploadService, ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _uploadService = uploadService;
        _context = context;
        _session = httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Session required");
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string fileType, [FromForm] string competence)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        var userId = _session.GetString("UserId");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User not authenticated" });

        try
        {
            using (var stream = file.OpenReadStream())
            {
                var importFile = await _uploadService.UploadFileAsync(stream, file.FileName, fileType, competence, userId);
                _context.ImportFiles.Add(importFile);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = importFile.Id,
                    fileName = importFile.FileName,
                    fileHash = importFile.FileHash,
                    fileType = importFile.FileType,
                    competence = importFile.Competence,
                    status = importFile.Status,
                    createdAt = importFile.CreatedAt
                });
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile(int id)
    {
        var importFile = await _context.ImportFiles.FindAsync(id);
        if (importFile == null)
            return NotFound();

        return Ok(new
        {
            id = importFile.Id,
            fileName = importFile.FileName,
            fileHash = importFile.FileHash,
            fileType = importFile.FileType,
            competence = importFile.Competence,
            status = importFile.Status,
            uploadedAt = importFile.CreatedAt
        });
    }

    [HttpGet]
    public IActionResult ListFiles([FromQuery] string? fileType = null, [FromQuery] string? competence = null)
    {
        var query = _context.ImportFiles.AsQueryable();

        if (!string.IsNullOrEmpty(fileType))
            query = query.Where(f => f.FileType == fileType.ToUpper());

        if (!string.IsNullOrEmpty(competence))
            query = query.Where(f => f.Competence == competence);

        var files = query.OrderByDescending(f => f.CreatedAt).Take(100).Select(f => new
        {
            id = f.Id,
            fileName = f.FileName,
            fileType = f.FileType,
            competence = f.Competence,
            status = f.Status,
            uploadedAt = f.CreatedAt
        }).ToList();

        return Ok(files);
    }
}
