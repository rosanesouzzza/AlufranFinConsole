using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using AlufranFinConsole.Domain.Entities;

namespace AlufranFinConsole.Application.Services;

public interface IFileUploadService
{
    Task<ImportFile> UploadFileAsync(Stream fileStream, string fileName, string fileType, string competence, int userId);
    string GenerateFileHash(Stream stream);
    bool ValidateFileType(string fileType);
}

public class FileUploadService : IFileUploadService
{
    private readonly string _storagePath;
    private static readonly string[] AllowedTypes = { "PAG", "REC", "FAT", "EMITIDAS", "COMP", "TRANSF", "FOPAG" };

    public FileUploadService(IConfiguration config)
    {
        _storagePath = config.GetSection("Storage:Path").Value ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<ImportFile> UploadFileAsync(Stream fileStream, string fileName, string fileType, string competence, int userId)
    {
        if (!ValidateFileType(fileType))
            throw new ArgumentException($"File type '{fileType}' is not supported");

        if (!ValidateCompetence(competence))
            throw new ArgumentException("Invalid competence format (use YYYY-MM)");

        var hash = GenerateFileHash(fileStream);
        fileStream.Position = 0;

        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var typeFolder = Path.Combine(_storagePath, fileType, dateFolder);
        Directory.CreateDirectory(typeFolder);

        var storageName = $"{hash}_{fileName}";
        var storagePath = Path.Combine(typeFolder, storageName);

        using (var fileHandle = new FileStream(storagePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileHandle);
        }

        return new ImportFile
        {
            FileName = fileName,
            FileHash = hash,
            FileType = fileType,
            Competence = competence,
            Status = "UPLOADED",
            StoragePath = Path.GetRelativePath(_storagePath, storagePath),
            UploadedBy_Id = userId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public string GenerateFileHash(Stream stream)
    {
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public bool ValidateFileType(string fileType)
    {
        return AllowedTypes.Contains(fileType?.ToUpperInvariant() ?? "");
    }

    private bool ValidateCompetence(string competence)
    {
        if (string.IsNullOrWhiteSpace(competence))
            return false;

        var parts = competence.Split('-');
        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], out var year) && year >= 2020 && year <= 2099 &&
               int.TryParse(parts[1], out var month) && month >= 1 && month <= 12;
    }
}
