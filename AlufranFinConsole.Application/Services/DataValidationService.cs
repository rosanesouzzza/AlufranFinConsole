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

    /// <summary>
    /// Parse line — short-circuits to the pre-parsed JSON when available (XLSX/PDF upload).
    /// Falls back to CSV ParseLine when parsedDataJson is null or empty.
    /// </summary>
    ParseResult ParseLineOrJson(string fileType, string rawData, string? parsedDataJson);
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

    /// <summary>
    /// When a StagingData record already has pre-parsed JSON (from XLSX/PDF upload),
    /// return it directly without re-parsing the raw CSV line.
    /// Falls back to ParseLine for CSV files.
    /// </summary>
    public ParseResult ParseLineOrJson(string fileType, string rawData, string? parsedDataJson)
    {
        if (!string.IsNullOrWhiteSpace(parsedDataJson))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(parsedDataJson);
                if (dict != null && dict.Count > 0)
                    return new ParseResult { Success = true, ParsedData = dict };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("ParseLineOrJson: invalid JSON in ParsedData, falling back to CSV. {Msg}", ex.Message);
            }
        }

        return ParseLine(fileType, rawData);
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
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
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
            { "valor", decimal.Parse(data.GetValueOrDefault("valor", "0"),
                           System.Globalization.CultureInfo.InvariantCulture) },
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
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
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
            { "valor", decimal.Parse(data.GetValueOrDefault("valor", "0"),
                           System.Globalization.CultureInfo.InvariantCulture) },
            { "data_recebimento", DateTime.Parse(data.GetValueOrDefault("data_recebimento", DateTime.Today.ToString())) }
        };
    }

    #endregion

    #region FAT (Faturamento — NF emitidas ao cliente)
    // Formato: numero_nf,cliente,valor,data_emissao,descricao

    private Dictionary<string, string> ParseFat(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "numero_nf",    fields[0]?.Trim() ?? "" },
            { "cliente",      fields[1]?.Trim() ?? "" },
            { "valor",        fields[2]?.Trim() ?? "" },
            { "data_emissao", fields[3]?.Trim() ?? "" },
            { "descricao",    fields[4]?.Trim() ?? "" }
        };
    }

    private List<string> ValidateFat(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(data.GetValueOrDefault("numero_nf")))
            errors.Add("numero_nf é obrigatório");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("cliente")))
            errors.Add("cliente é obrigatório");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            errors.Add("valor deve ser número positivo");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_emissao"), out _))
            errors.Add("data_emissao deve ser data válida (YYYY-MM-DD)");
        return errors;
    }

    private Dictionary<string, object> SanitizeFat(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "numero_nf",          data.GetValueOrDefault("numero_nf", "").ToUpper() },
            { "cliente_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("cliente", "")) },
            { "valor",              decimal.Parse(data.GetValueOrDefault("valor", "0"),
                                        System.Globalization.CultureInfo.InvariantCulture) },
            { "data_emissao",       DateTime.Parse(data.GetValueOrDefault("data_emissao", "")) },
            { "descricao",          data.GetValueOrDefault("descricao", "").Trim() }
        };
    }

    #endregion

    #region EMITIDAS (Duplicatas / Títulos a Receber)
    // Formato: numero_doc,cliente,valor,data_vencimento,data_emissao,status

    private Dictionary<string, string> ParseEmitidas(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "numero_doc",       fields[0]?.Trim() ?? "" },
            { "cliente",          fields[1]?.Trim() ?? "" },
            { "valor",            fields[2]?.Trim() ?? "" },
            { "data_vencimento",  fields[3]?.Trim() ?? "" },
            { "data_emissao",     fields[4]?.Trim() ?? "" },
            { "status",           fields.Length > 5 ? fields[5]?.Trim() ?? "ABERTO" : "ABERTO" }
        };
    }

    private static readonly string[] StatusEmitidas = { "ABERTO", "PAGO", "CANCELADO", "VENCIDO" };

    private List<string> ValidateEmitidas(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(data.GetValueOrDefault("numero_doc")))
            errors.Add("numero_doc é obrigatório");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("cliente")))
            errors.Add("cliente é obrigatório");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            errors.Add("valor deve ser número positivo");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_vencimento"), out _))
            errors.Add("data_vencimento deve ser data válida (YYYY-MM-DD)");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_emissao"), out _))
            errors.Add("data_emissao deve ser data válida (YYYY-MM-DD)");
        var status = data.GetValueOrDefault("status", "ABERTO").ToUpper();
        if (!StatusEmitidas.Contains(status))
            errors.Add($"status inválido. Valores aceitos: {string.Join(", ", StatusEmitidas)}");
        return errors;
    }

    private Dictionary<string, object> SanitizeEmitidas(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "numero_doc",          data.GetValueOrDefault("numero_doc", "").ToUpper() },
            { "cliente_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("cliente", "")) },
            { "valor",               decimal.Parse(data.GetValueOrDefault("valor", "0"),
                                         System.Globalization.CultureInfo.InvariantCulture) },
            { "data_vencimento",     DateTime.Parse(data.GetValueOrDefault("data_vencimento", "")) },
            { "data_emissao",        DateTime.Parse(data.GetValueOrDefault("data_emissao", "")) },
            { "status",              data.GetValueOrDefault("status", "ABERTO").ToUpper() }
        };
    }

    #endregion

    #region COMP (Compras — NF de entrada / fornecedores)
    // Formato: numero_nf,fornecedor,valor,data_entrada,categoria,descricao

    private Dictionary<string, string> ParseComp(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "numero_nf",    fields[0]?.Trim() ?? "" },
            { "fornecedor",   fields[1]?.Trim() ?? "" },
            { "valor",        fields[2]?.Trim() ?? "" },
            { "data_entrada", fields[3]?.Trim() ?? "" },
            { "categoria",    fields[4]?.Trim() ?? "" },
            { "descricao",    fields.Length > 5 ? fields[5]?.Trim() ?? "" : "" }
        };
    }

    private List<string> ValidateComp(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(data.GetValueOrDefault("numero_nf")))
            errors.Add("numero_nf é obrigatório");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("fornecedor")))
            errors.Add("fornecedor é obrigatório");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            errors.Add("valor deve ser número positivo");
        if (!DateTime.TryParse(data.GetValueOrDefault("data_entrada"), out _))
            errors.Add("data_entrada deve ser data válida (YYYY-MM-DD)");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("categoria")))
            errors.Add("categoria é obrigatória");
        return errors;
    }

    private Dictionary<string, object> SanitizeComp(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "numero_nf",              data.GetValueOrDefault("numero_nf", "").ToUpper() },
            { "fornecedor_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("fornecedor", "")) },
            { "valor",                  decimal.Parse(data.GetValueOrDefault("valor", "0"),
                                            System.Globalization.CultureInfo.InvariantCulture) },
            { "data_entrada",           DateTime.Parse(data.GetValueOrDefault("data_entrada", "")) },
            { "categoria",              data.GetValueOrDefault("categoria", "").ToUpper().Trim() },
            { "descricao",              data.GetValueOrDefault("descricao", "").Trim() }
        };
    }

    #endregion

    #region TRANSF (Transferências Bancárias)
    // Formato: numero_doc,conta_origem,conta_destino,valor,data,descricao

    private Dictionary<string, string> ParseTransf(string[] fields)
    {
        if (fields.Length < 5) return null;
        return new Dictionary<string, string>
        {
            { "numero_doc",     fields[0]?.Trim() ?? "" },
            { "conta_origem",   fields[1]?.Trim() ?? "" },
            { "conta_destino",  fields[2]?.Trim() ?? "" },
            { "valor",          fields[3]?.Trim() ?? "" },
            { "data",           fields[4]?.Trim() ?? "" },
            { "descricao",      fields.Length > 5 ? fields[5]?.Trim() ?? "" : "" }
        };
    }

    private List<string> ValidateTransf(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(data.GetValueOrDefault("numero_doc")))
            errors.Add("numero_doc é obrigatório");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("conta_origem")))
            errors.Add("conta_origem é obrigatória");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("conta_destino")))
            errors.Add("conta_destino é obrigatória");
        var co = data.GetValueOrDefault("conta_origem", "");
        var cd = data.GetValueOrDefault("conta_destino", "");
        if (!string.IsNullOrEmpty(co) && co == cd)
            errors.Add("conta_origem e conta_destino não podem ser iguais");
        if (!decimal.TryParse(data.GetValueOrDefault("valor"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            errors.Add("valor deve ser número positivo");
        if (!DateTime.TryParse(data.GetValueOrDefault("data"), out _))
            errors.Add("data deve ser data válida (YYYY-MM-DD)");
        return errors;
    }

    private Dictionary<string, object> SanitizeTransf(Dictionary<string, string> data)
    {
        return new Dictionary<string, object>
        {
            { "numero_doc",    data.GetValueOrDefault("numero_doc", "").ToUpper() },
            { "conta_origem",  data.GetValueOrDefault("conta_origem", "").ToUpper().Trim() },
            { "conta_destino", data.GetValueOrDefault("conta_destino", "").ToUpper().Trim() },
            { "valor",         decimal.Parse(data.GetValueOrDefault("valor", "0"),
                                   System.Globalization.CultureInfo.InvariantCulture) },
            { "data",          DateTime.Parse(data.GetValueOrDefault("data", "")) },
            { "descricao",     data.GetValueOrDefault("descricao", "").Trim() }
        };
    }

    #endregion

    #region FOPAG (Folha de Pagamento)
    // Formato: matricula,funcionario,cargo,valor_bruto,descontos,valor_liquido,competencia

    private Dictionary<string, string> ParseFopag(string[] fields)
    {
        if (fields.Length < 6) return null;
        return new Dictionary<string, string>
        {
            { "matricula",     fields[0]?.Trim() ?? "" },
            { "funcionario",   fields[1]?.Trim() ?? "" },
            { "cargo",         fields[2]?.Trim() ?? "" },
            { "valor_bruto",   fields[3]?.Trim() ?? "" },
            { "descontos",     fields[4]?.Trim() ?? "" },
            { "valor_liquido", fields[5]?.Trim() ?? "" },
            { "competencia",   fields.Length > 6 ? fields[6]?.Trim() ?? "" : "" }
        };
    }

    private List<string> ValidateFopag(Dictionary<string, string> data)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(data.GetValueOrDefault("matricula")))
            errors.Add("matricula é obrigatória");
        if (string.IsNullOrEmpty(data.GetValueOrDefault("funcionario")))
            errors.Add("funcionario é obrigatório");
        if (!decimal.TryParse(data.GetValueOrDefault("valor_bruto"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var bruto) || bruto <= 0)
            errors.Add("valor_bruto deve ser número positivo");
        if (!decimal.TryParse(data.GetValueOrDefault("descontos"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var desc) || desc < 0)
            errors.Add("descontos deve ser número não-negativo");
        if (!decimal.TryParse(data.GetValueOrDefault("valor_liquido"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var liq) || liq < 0)
            errors.Add("valor_liquido deve ser número não-negativo");
        // Consistência: bruto - descontos ≈ liquido (tolerância R$0,01)
        if (errors.Count == 0 && Math.Abs(bruto - desc - liq) > 0.01m)
            errors.Add($"inconsistência: valor_bruto ({bruto}) - descontos ({desc}) ≠ valor_liquido ({liq})");
        var comp = data.GetValueOrDefault("competencia", "");
        if (!string.IsNullOrEmpty(comp) && !Regex.IsMatch(comp, @"^\d{4}-\d{2}$"))
            errors.Add("competencia deve estar no formato YYYY-MM");
        return errors;
    }

    private Dictionary<string, object> SanitizeFopag(Dictionary<string, string> data)
    {
        var bruto = decimal.Parse(data.GetValueOrDefault("valor_bruto", "0"),
                        System.Globalization.CultureInfo.InvariantCulture);
        var desc  = decimal.Parse(data.GetValueOrDefault("descontos", "0"),
                        System.Globalization.CultureInfo.InvariantCulture);
        return new Dictionary<string, object>
        {
            { "matricula",             data.GetValueOrDefault("matricula", "").ToUpper().Trim() },
            { "funcionario_normalizado", _normalizationService.NormalizeForKey(data.GetValueOrDefault("funcionario", "")) },
            { "cargo",                 data.GetValueOrDefault("cargo", "").Trim() },
            { "valor_bruto",           bruto },
            { "descontos",             desc },
            { "valor_liquido",         bruto - desc },   // recalculado para garantir consistência
            { "competencia",           data.GetValueOrDefault("competencia", "").Trim() }
        };
    }

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
