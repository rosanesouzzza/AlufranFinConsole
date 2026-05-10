using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class StagingModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StagingModel> _logger;

    public List<ImportFile>? ImportFiles { get; set; }
    public List<StagingRecord>? StagingRecords { get; set; }
    public StagingSummary? Summary { get; set; }
    public int SelectedFileId { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public StagingModel(IHttpClientFactory httpClientFactory, ILogger<StagingModel> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadImportFiles();
    }

    public async Task OnPostSelectFileAsync(int importFileId)
    {
        SelectedFileId = importFileId;
        await LoadImportFiles();
        if (importFileId > 0)
        {
            await LoadStagingData(importFileId);
            await LoadSummary(importFileId);
        }
    }

    public async Task OnPostValidateAsync(int importFileId)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Não autenticado. Faça login novamente.";
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsync($"/api/staging/{importFileId}/validate", null);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<dynamic>(content);
                SuccessMessage = $"Validação concluída: {content}";
            }
            else
            {
                ErrorMessage = $"Erro ao validar: {response.StatusCode}";
            }

            await OnPostSelectFileAsync(importFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro na validação: {ex.Message}");
            ErrorMessage = $"Erro: {ex.Message}";
            await LoadImportFiles();
        }
    }

    public async Task OnPostSanitizeAsync(int importFileId)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Não autenticado. Faça login novamente.";
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsync($"/api/staging/{importFileId}/sanitize", null);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                SuccessMessage = $"Saneamento concluído: {content}";
            }
            else
            {
                ErrorMessage = $"Erro ao sanitizar: {response.StatusCode}";
            }

            await OnPostSelectFileAsync(importFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro no saneamento: {ex.Message}");
            ErrorMessage = $"Erro: {ex.Message}";
            await LoadImportFiles();
        }
    }

    public async Task OnPostReportAsync(int importFileId)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Não autenticado. Faça login novamente.";
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"/api/staging/{importFileId}/report");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                SuccessMessage = $"Relatório gerado com sucesso";
            }
            else
            {
                ErrorMessage = $"Erro ao gerar relatório: {response.StatusCode}";
            }

            await OnPostSelectFileAsync(importFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao gerar relatório: {ex.Message}");
            ErrorMessage = $"Erro: {ex.Message}";
            await LoadImportFiles();
        }
    }

    private async Task LoadImportFiles()
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Não autenticado. Faça login para continuar.";
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Assumindo que existe um endpoint para listar arquivos de importação
            // Se não existir, será necessário criar
            var response = await _httpClient.GetAsync("/api/import/list");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("files", out var filesArray))
                {
                    ImportFiles = new List<ImportFile>();
                    foreach (var file in filesArray.EnumerateArray())
                    {
                        ImportFiles.Add(new ImportFile
                        {
                            Id = file.GetProperty("id").GetInt32(),
                            FileName = file.GetProperty("fileName").GetString() ?? "",
                            FileType = file.GetProperty("fileType").GetString() ?? "",
                            CreatedAt = file.GetProperty("createdAt").GetDateTime()
                        });
                    }
                }
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Erro ao carregar arquivos: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao carregar arquivos: {ex.Message}");
        }
    }

    private async Task LoadStagingData(int importFileId)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"/api/staging/{importFileId}?limit=50");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                StagingRecords = new List<StagingRecord>();
                if (root.TryGetProperty("records", out var recordsArray))
                {
                    foreach (var record in recordsArray.EnumerateArray())
                    {
                        StagingRecords.Add(new StagingRecord
                        {
                            Id = record.GetProperty("id").GetInt32(),
                            LineNumber = record.GetProperty("lineNumber").GetInt32(),
                            RawData = record.GetProperty("rawData").GetString() ?? "",
                            ValidationStatus = record.GetProperty("validationStatus").GetString() ?? "PENDING",
                            ValidationErrors = record.GetProperty("validationErrors").GetString() ?? "",
                            CreatedAt = record.GetProperty("createdAt").GetDateTime()
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao carregar dados de staging: {ex.Message}");
        }
    }

    private async Task LoadSummary(int importFileId)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync($"/api/staging/{importFileId}/summary");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                Summary = new StagingSummary
                {
                    FileName = root.GetProperty("fileName").GetString() ?? "",
                    TotalLines = root.GetProperty("totalLines").GetInt32(),
                    Pending = root.GetProperty("pending").GetInt32(),
                    Valid = root.GetProperty("valid").GetInt32(),
                    Invalid = root.GetProperty("invalid").GetInt32(),
                    Duplicate = root.GetProperty("duplicate").GetInt32(),
                    Processed = root.GetProperty("processed").GetInt32(),
                    PercentValid = root.GetProperty("percentValid").GetDouble()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao carregar resumo: {ex.Message}");
        }
    }
}

public class ImportFile
{
    public int Id { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StagingRecord
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string? RawData { get; set; }
    public string? ValidationStatus { get; set; }
    public string? ValidationErrors { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StagingSummary
{
    public string? FileName { get; set; }
    public int TotalLines { get; set; }
    public int Pending { get; set; }
    public int Valid { get; set; }
    public int Invalid { get; set; }
    public int Duplicate { get; set; }
    public int Processed { get; set; }
    public double PercentValid { get; set; }
}
