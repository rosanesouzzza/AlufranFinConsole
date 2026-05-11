using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class StagingModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StagingModel> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public List<ImportFile>? ImportFiles { get; set; }
    public List<StagingRecord>? StagingRecords { get; set; }
    public StagingSummary? Summary { get; set; }
    public int SelectedFileId { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAuthenticated { get; set; }

    [BindProperty]
    public string? LoginEmail { get; set; }

    [BindProperty]
    public string? LoginPassword { get; set; }

    public StagingModel(IHttpClientFactory httpClientFactory, ILogger<StagingModel> logger,
        IWebHostEnvironment env, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _logger = logger;
        _env = env;
        _config = config;
    }

    public async Task OnGetAsync()
    {
        CheckAuthentication();

        // Auto-login em modo Development para facilitar testes locais
        if (!IsAuthenticated && _env.IsDevelopment())
        {
            await TryAutoLoginAsync();
        }

        if (IsAuthenticated)
        {
            await LoadImportFiles();
        }
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            var email = _config["DevLogin:Email"] ?? "admin@alufran.local";
            var password = _config["DevLogin:Password"] ?? "AlufranAdmin@2026";

            var loginRequest = new { email, password };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    HttpContext.Session.SetString("AuthToken", token!);
                    HttpContext.Session.SetString("UserEmail", email);
                    IsAuthenticated = true;
                    _logger.LogInformation("Auto-login em modo Development realizado para {Email}.", email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Auto-login falhou: {Msg}", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (string.IsNullOrEmpty(LoginEmail) || string.IsNullOrEmpty(LoginPassword))
        {
            ErrorMessage = "Email e senha são obrigatórios.";
            CheckAuthentication();
            return Page();
        }

        try
        {
            var loginRequest = new { email = LoginEmail, password = LoginPassword };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();

                    HttpContext.Session.SetString("AuthToken", token!);
                    HttpContext.Session.SetString("UserEmail", LoginEmail);

                    // Persistir em cookie HttpOnly para sobreviver a restarts do servidor
                    var cookieOpts = new Microsoft.AspNetCore.Http.CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(8)
                    };
                    Response.Cookies.Append("AlufranAuthToken", token!, cookieOpts);
                    Response.Cookies.Append("AlufranUserEmail", LoginEmail!, cookieOpts);

                    _logger.LogInformation($"Usuário {LoginEmail} autenticado com sucesso na página Staging.");

                    CheckAuthentication();
                    await LoadImportFiles();
                    SuccessMessage = "Login realizado com sucesso!";
                    return Page();
                }
                else
                {
                    ErrorMessage = "Resposta de autenticação inválida.";
                    CheckAuthentication();
                    return Page();
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Email ou senha inválidos.";
                _logger.LogWarning($"Tentativa de login falhou para {LoginEmail} na página Staging.");
                CheckAuthentication();
                return Page();
            }
            else
            {
                ErrorMessage = $"Erro na autenticação: {response.StatusCode}";
                _logger.LogError($"Erro ao autenticar na página Staging: {response.StatusCode}");
                CheckAuthentication();
                return Page();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro na autenticação na página Staging: {ex.Message}");
            ErrorMessage = $"Erro ao conectar com o servidor: {ex.Message}";
            CheckAuthentication();
            return Page();
        }
    }

    private void CheckAuthentication()
    {
        var token = HttpContext.Session.GetString("AuthToken");

        // Fallback: restaurar sessão a partir do cookie persistente (sobrevive a restarts)
        if (string.IsNullOrEmpty(token))
        {
            token = Request.Cookies["AlufranAuthToken"];
            if (!string.IsNullOrEmpty(token))
            {
                HttpContext.Session.SetString("AuthToken", token);
                var email = Request.Cookies["AlufranUserEmail"] ?? "";
                if (!string.IsNullOrEmpty(email))
                    HttpContext.Session.SetString("UserEmail", email);
                _logger.LogInformation("Sessão restaurada a partir do cookie persistente.");
            }
        }

        IsAuthenticated = !string.IsNullOrEmpty(token);
    }

    /// <summary>
    /// Garante autenticação em todos os handlers — inclui auto-login em Development.
    /// Retorna o token JWT ou null se não autenticado.
    /// </summary>
    private async Task<string?> EnsureAuthenticatedAsync()
    {
        CheckAuthentication();
        if (!IsAuthenticated && _env.IsDevelopment())
            await TryAutoLoginAsync();

        var token = HttpContext.Session.GetString("AuthToken");
        if (string.IsNullOrEmpty(token))
            ErrorMessage = "Não autenticado. Por favor, faça login.";
        return token;
    }

    public async Task OnPostSelectFileAsync(int importFileId)
    {
        await EnsureAuthenticatedAsync();
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
            var token = await EnsureAuthenticatedAsync();
            if (string.IsNullOrEmpty(token))
            {
                await LoadImportFiles();
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
            var token = await EnsureAuthenticatedAsync();
            if (string.IsNullOrEmpty(token)) { await LoadImportFiles(); return; }

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
            var token = await EnsureAuthenticatedAsync();
            if (string.IsNullOrEmpty(token)) { await LoadImportFiles(); return; }

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
