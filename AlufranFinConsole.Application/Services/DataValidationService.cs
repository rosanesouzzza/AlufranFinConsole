using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AlufranFinConsole.Application.Services;

/// <summary>
/// Service for validating and sanitizing financial data from imported files
/// </summary>
public interface IDataValidationService
{
    /// <summary>
    /// Parse a CSV line into fields based on file type
    /// </summary>
    ParseResult ParseLine(string fileType, string rawData);

    /// <summary>
    /// Validate parsed fields according to business rules
    /// </summary>
    ValidationResult ValidateParsedData(string fileType, Dictionary<string, string> parsedData);

    /// <summary>
    /// Sanitize valid data (normalize values, apply transformations)
    /// </summary>
    SanitizeResult SanitizeData(string fileType, Dictionary<string, string> parsedData);

    /// <summary>
    /// Check for duplicate records
    /// </summary>
    Task<bool> IsDuplicateAsync(string fileType, Dictionary<string, string> parsedData);
}

public class DataValidationService : IDataValidationService
{
    private readonly ITextNormalizationService _normalizationService;
    private readonly ILogger<DataValidationService> _logger;

    public DataValidationService(
        ITextNormalizationService normalizationService,
        ILogger<DataValidationService> logger)
    {
        _normalizationService = normalizationService;
        _logger = logger;
    }

    /// <summary>
    /// Parse CSV line based on file type
    /// Supported formats: PAG, REC, FAT, EMITIDAS, COMP, TRANSF, FOPAG
    /// </summary>
    public ParseResult ParseLine(string fileType, string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            return new ParseResult { Success = false, Error = "Empty line" };

        try
        {
            var fields = rawData.Split(',');
            var parsed = fileType.ToUpper() switch
            {
                "PAG" => ParsePag(fields),
                "REC" => ParseRec(fields),
                "FAT" => ParseFat(fields),
                "EMITIDAS" => ParseEmitidas(fields),
                "COMP" => ParseComp(fields),
                "TRANSF" => ParseTransf(fields),
                "FOPAG" => ParseFopag(fields),
                _ => throw new InvalidOperationException($"Unsupported file type: {fileType}")
            };

            if (parsed == null || parsed.Count == 0)
                return new ParseResult { Success = false, Error = "Could not parse line" };

            return new ParseResult
            {
                Success = true,
                ParsedData = parsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Parse error for {fileType}: {ex.Message}");
            return new ParseResult { Success = false, Error = $"Parse failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Validate parsed data against business rules
    /// </summary>
    public ValidationResult ValidateParsedData(string fileType, Dictionary<string, string> parsedData)
    {
        var errors = new List<string>();

        // Common validations
        if (parsedData == null || parsedData.Count == 0)
            errors.Add("No data to validate");

        // File-type specific validations
        var typeErrors = fileType.ToUpper() switch
        {
            "PAG" => ValidatePag(parsedData),
            "REC" => ValidateRec(parsedData),
            "FAT" => ValidateFat(parsedData),
            "EMITIDAS" => ValidateEmitidas(parsedData),
            "COMP" => ValidateComp(parsedData),
            "TRANSF" => ValidateTransf(parsedData),
            "FOPAG" => ValidateFopag(parsedData),
            _ => new List<string> { $"Unknown file type: {fileType}" }
        };

        errors.AddRange(typeErrors);

        return new ValidationResult
        {
            Success = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Sanitize valid data (normalize, apply transformations)
    /// </summary>
    public SanitizeResult SanitizeData(string fileType, Dictionary<string, string> parsedData)
    {
        try
        {
            var sanitized = new Dictionary<string, object>();

            // Apply type-specific sanitization
            var typeResult = fileType.ToUpper() switch
            {
                "PAG" => SanitizePag(parsedData),
                "REC" => SanitizeRec(parsedData),
                "FAT" => SanitizeFat(parsedData),
                "EMITIDAS" => SanitizeEmitidas(parsedData),
                "COMP" => SanitizeComp(parsedData),
                "TRANSF" => SanitizeTransf(parsedData),
                "FOPAG" => SanitizeFopag(parsedData),
                _ => throw new InvalidOperationException($"Unknown file type: {fileType}")
            };

            return new SanitizeResult
            {
                Success = true,
                SanitizedData = typeResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Sanitize error: {ex.Message}");
            return new SanitizeResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Check for duplicates (stub - implement with context)
    /// </summary>
    public async Task<bool> IsDuplicateAsync(string fileType, Dictionary<string, string> parsedData)
    {
        // TODO: Implement duplicate detection with database context
        // This would check against existing consolidated records
        return await Task.FromResult(false);
    }

    #region PAG (Pagamentos)

    private Dictionary<string, string> ParsePag(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "documento", fields[0]?.Trim() ?? "" },
            { "fornecedor", fields[1]?.Trim() ?? "" },
            { "valor", fields[2]?.Trim() ?? "" },
            { "data_pagamento", fields[3]?.Trim() ?? "" },
            { "descricao", fields[4]?.Trim() ?? "" }
        };
    }

    private List<string> ValidatePag(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (!data.ContainsKey("documento") || string.IsNullOrEmpty(data["documento"]))
            errors.Add("documento is required");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), out var valor) || valor <= 0)
            errors.Add("valor must be positive number");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_pagamento"), out _))
            errors.Add("data_pagamento must be valid date");
        return errors;
    }

    private Dictionary<string, object> SanitizePag(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "documento", data.GetValueOrDefault("documento", "")?.ToUpper() ?? "" },
            { "fornecedor_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("fornecedor", "")) },
            { "valor", decimal.Parse(data.GetValueOrDefault("valor", "0")) },
            { "data_pagamento", DateTime.Parse(data.GetValueOrDefault("data_pagamento", DateTime.Today.ToString())) }
        };
    }

    #endregion

    #region REC (Recebimentos)

    private Dictionary<string, string> ParseRec(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "documento", fields[0]?.Trim() ?? "" },
            { "cliente", fields[1]?.Trim() ?? "" },
            { "valor", fields[2]?.Trim() ?? "" },
            { "data_recebimento", fields[3]?.Trim() ?? "" },
            { "descricao", fields[4]?.Trim() ?? "" }
        };
    }

    private List<string> ValidateRec(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (!data.ContainsKey("documento") || string.IsNullOrEmpty(data["documento"]))
            errors.Add("documento is required");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), out var valor) || valor <= 0)
            errors.Add("valor must be positive number");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_recebimento"), out _))
            errors.Add("data_recebimento must be valid date");
        return errors;
    }

    private Dictionary<string, object> SanitizeRec(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "documento", data.GetValueOrDefault("documento", "")?.ToUpper() ?? "" },
            { "cliente_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("cliente", "")) },
            { "valor", decimal.Parse(data.GetValueOrDefault("valor", "0")) },
            { "data_recebimento", DateTime.Parse(data.GetValueOrDefault("data_recebimento", DateTime.Today.ToString())) }
        };
    }

    #endregion

    #region Stubs para FAT, EMITIDAS, COMP, TRANSF, FOPAG

    private Dictionary<string, string> ParseFat(string[] fields) => fields.Length >= 3 ? new() { { "raw", string.Join(",", fields) } } : null;
    private List<string> ValidateFat(Dictionary<string, string> data) => new();
    private Dictionary<string, object> SanitizeFat(Dictionary<string, string> data) => new();

    private Dictionary<string, string> ParseEmitidas(string[] fields) => fields.Length >= 3 ? new() { { "raw", string.Join(",", fields) } } : null;
    private List<string> ValidateEmitidas(Dictionary<string, string> data) => new();
    private Dictionary<string, object> SanitizeEmitidas(Dictionary<string, string> data) => new();

    private Dictionary<string, string> ParseComp(string[] fields) => fields.Length >= 3 ? new() { { "raw", string.Join(",", fields) } } : null;
    private List<string> ValidateComp(Dictionary<string, string> data) => new();
    private Dictionary<string, object> SanitizeComp(Dictionary<string, string> data) => new();

    private Dictionary<string, string> ParseTransf(string[] fields) => fields.Length >= 3 ? new() { { "raw", string.Join(",", fields) } } : null;
    private List<string> ValidateTransf(Dictionary<string, string> data) => new();
    private Dictionary<string, object> SanitizeTransf(Dictionary<string, string> data) => new();

    private Dictionary<string, string> ParseFopag(string[] fields) => fields.Length >= 3 ? new() { { "raw", string.Join(",", fields) } } : null;
    private List<string> ValidateFopag(Dictionary<string, string> data) => new();
    private Dictionary<string, object> SanitizeFopag(Dictionary<string, string> data) => new();

    #endregion
}

public class ParseResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> ParsedData { get; set; }
    public string Error { get; set; }
}

public class ValidationResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class SanitizeResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> SanitizedData { get; set; }
    public string Error { get; set; }
}
