using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class UploadModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadModel> _logger;

    public List<UploadedFile>? ImportFiles { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAuthenticated { get; set; }

    public UploadModel(IHttpClientFactory httpClientFactory, ILogger<UploadModel> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        CheckAuthentication();
        if (IsAuthenticated)
            await LoadFiles();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile file, string fileType, string competence)
    {
        CheckAuthentication();
        if (!IsAuthenticated)
        {
            ErrorMessage = "Não autenticado. Faça login na página de Staging primeiro.";
            return Page();
        }

        if (file == null || file.Length == 0)
        {
            ErrorMessage = "Selecione um arquivo válido.";
            await LoadFiles();
            return Page();
        }

        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!);

            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

            content.Add(fileContent, "file", file.FileName);
            content.Add(new StringContent(fileType), "fileType");
            // Convert month input (YYYY-MM) to required format
            content.Add(new StringContent(competence), "competence");

            var response = await _httpClient.PostAsync("/api/upload", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var id = doc.RootElement.GetProperty("id").GetInt32();
                SuccessMessage = $"Arquivo '{file.FileName}' enviado com sucesso! ID: {id}. Acesse o Staging para validar.";
                _logger.LogInformation($"Upload successful: {file.FileName} as {fileType}/{competence}, ID={id}");
            }
            else
            {
                // Try to extract error message from JSON
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var errMsg = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : responseBody;
                    ErrorMessage = $"Erro no upload: {errMsg}";
                }
                catch
                {
                    ErrorMessage = $"Erro no upload ({(int)response.StatusCode}): {responseBody}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Upload error: {ex.Message}");
            ErrorMessage = $"Erro ao enviar arquivo: {ex.Message}";
        }

        await LoadFiles();
        return Page();
    }

    private void CheckAuthentication()
    {
        var token = HttpContext.Session.GetString("AuthToken");

        // Fallback: restaurar sessão a partir do cookie persistente
        if (string.IsNullOrEmpty(token))
        {
            token = Request.Cookies["AlufranAuthToken"];
            if (!string.IsNullOrEmpty(token))
            {
                HttpContext.Session.SetString("AuthToken", token);
                var email = Request.Cookies["AlufranUserEmail"] ?? "";
                if (!string.IsNullOrEmpty(email))
                    HttpContext.Session.SetString("UserEmail", email);
            }
        }

        IsAuthenticated = !string.IsNullOrEmpty(token);
    }

    private async Task LoadFiles()
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token)) return;

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("/api/import/list");
            if (!response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("files", out var arr))
            {
                ImportFiles = new List<UploadedFile>();
                foreach (var f in arr.EnumerateArray())
                {
                    ImportFiles.Add(new UploadedFile
                    {
                        Id = f.GetProperty("id").GetInt32(),
                        FileName = f.GetProperty("fileName").GetString() ?? "",
                        FileType = f.GetProperty("fileType").GetString() ?? "",
                        Competence = f.TryGetProperty("competence", out var comp) ? comp.GetString() ?? "" : "",
                        Status = f.GetProperty("status").GetString() ?? "PENDING",
                        CreatedAt = f.GetProperty("createdAt").GetDateTime()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading files: {ex.Message}");
        }
    }
}

public class UploadedFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public string Competence { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
