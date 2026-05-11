using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class ProcessingModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProcessingModel> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    // ── Dados exibidos na view ─────────────────────────────────────────
    public List<ProcessingFile>? ImportFiles { get; set; }
    public ClosingSummaryDto? Summary { get; set; }
    public List<TransactionDto>? Transactions { get; set; }

    [BindProperty]
    public string SelectedCompetence { get; set; } = DateTime.Now.ToString("yyyy-MM");

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAuthenticated { get; set; }

    [BindProperty]
    public string? LoginEmail { get; set; }
    [BindProperty]
    public string? LoginPassword { get; set; }

    public ProcessingModel(IHttpClientFactory httpClientFactory, ILogger<ProcessingModel> logger,
        IWebHostEnvironment env, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _logger = logger;
        _env = env;
        _config = config;
    }

    // ── GET ──────────────────────────────────────────────────────────────
    public async Task OnGetAsync([FromQuery] string? competence = null)
    {
        CheckAuthentication();
        if (!IsAuthenticated && _env.IsDevelopment())
            await TryAutoLoginAsync();

        if (!string.IsNullOrEmpty(competence))
            SelectedCompetence = competence;

        if (IsAuthenticated)
        {
            await LoadImportFiles();
            await LoadSummary(SelectedCompetence);
            await LoadTransactions(SelectedCompetence);
        }
    }

    // ── POST: Login ──────────────────────────────────────────────────────
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
            var resp = await _httpClient.PostAsync("/api/auth/login",
                new StringContent(JsonSerializer.Serialize(new { email = LoginEmail, password = LoginPassword }),
                    System.Text.Encoding.UTF8, "application/json"));

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("token", out var tok))
                {
                    var token = tok.GetString()!;
                    HttpContext.Session.SetString("AuthToken", token);
                    HttpContext.Session.SetString("UserEmail", LoginEmail);
                    var cookieOpts = new Microsoft.AspNetCore.Http.CookieOptions
                    { HttpOnly = true, Secure = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                      Expires = DateTimeOffset.UtcNow.AddHours(8) };
                    Response.Cookies.Append("AlufranAuthToken", token, cookieOpts);
                    Response.Cookies.Append("AlufranUserEmail", LoginEmail, cookieOpts);
                    CheckAuthentication();
                    await LoadImportFiles();
                    await LoadSummary(SelectedCompetence);
                    await LoadTransactions(SelectedCompetence);
                    SuccessMessage = "Login realizado com sucesso!";
                    return Page();
                }
            }
            ErrorMessage = resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Email ou senha inválidos." : $"Erro: {resp.StatusCode}";
        }
        catch (Exception ex) { ErrorMessage = $"Erro ao conectar: {ex.Message}"; }
        CheckAuthentication();
        return Page();
    }

    // ── POST: Selecionar competência ─────────────────────────────────────
    public async Task OnPostSelectCompetenceAsync()
    {
        await EnsureAuthenticatedAsync();
        await LoadImportFiles();
        await LoadSummary(SelectedCompetence);
        await LoadTransactions(SelectedCompetence);
    }

    // ── POST: Processar arquivo ──────────────────────────────────────────
    public async Task OnPostProcessAsync(int importFileId)
    {
        var token = await EnsureAuthenticatedAsync();
        if (string.IsNullOrEmpty(token)) { await LoadImportFiles(); return; }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _httpClient.PostAsync($"/api/processing/{importFileId}/process", null);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var created = doc.RootElement.TryGetProperty("created", out var c) ? c.GetInt32() : 0;
                var skipped = doc.RootElement.TryGetProperty("skipped", out var s) ? s.GetInt32() : 0;
                SuccessMessage = $"✅ Processamento concluído: {created} transações criadas, {skipped} ignoradas.";
            }
            else
            {
                using var doc = JsonDocument.Parse(body);
                var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : body;
                ErrorMessage = $"Erro: {err}";
            }
        }
        catch (Exception ex) { ErrorMessage = $"Erro: {ex.Message}"; }

        await LoadImportFiles();
        await LoadSummary(SelectedCompetence);
        await LoadTransactions(SelectedCompetence);
    }

    // ── POST: Cancelar processamento ─────────────────────────────────────
    public async Task OnPostCancelAsync(int importFileId)
    {
        var token = await EnsureAuthenticatedAsync();
        if (string.IsNullOrEmpty(token)) { await LoadImportFiles(); return; }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _httpClient.DeleteAsync($"/api/processing/{importFileId}/cancel");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                var removed = doc.RootElement.TryGetProperty("removedTransactions", out var r) ? r.GetInt32() : 0;
                SuccessMessage = $"↩️ Processamento cancelado: {removed} transações removidas. Arquivo revertido para VALID.";
            }
            else
            {
                using var doc = JsonDocument.Parse(body);
                var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : body;
                ErrorMessage = $"Erro ao cancelar: {err}";
            }
        }
        catch (Exception ex) { ErrorMessage = $"Erro: {ex.Message}"; }

        await LoadImportFiles();
        await LoadSummary(SelectedCompetence);
        await LoadTransactions(SelectedCompetence);
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private async Task<string?> EnsureAuthenticatedAsync()
    {
        CheckAuthentication();
        if (!IsAuthenticated && _env.IsDevelopment())
            await TryAutoLoginAsync();
        var token = HttpContext.Session.GetString("AuthToken");
        if (string.IsNullOrEmpty(token))
            ErrorMessage = "Não autenticado. Faça login para continuar.";
        return token;
    }

    private void CheckAuthentication()
    {
        var token = HttpContext.Session.GetString("AuthToken");
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

    private async Task TryAutoLoginAsync()
    {
        try
        {
            var email = _config["DevLogin:Email"] ?? "admin@alufran.local";
            var password = _config["DevLogin:Password"] ?? "AlufranAdmin@2026";
            var resp = await _httpClient.PostAsync("/api/auth/login",
                new StringContent(JsonSerializer.Serialize(new { email, password }),
                    System.Text.Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("token", out var tok))
                {
                    HttpContext.Session.SetString("AuthToken", tok.GetString()!);
                    HttpContext.Session.SetString("UserEmail", email);
                    IsAuthenticated = true;
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning("Auto-login falhou: {Msg}", ex.Message); }
    }

    private async Task LoadImportFiles()
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token)) return;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.GetAsync("/api/upload?limit=200");
            if (!resp.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("files", out var arr)) return;

            ImportFiles = new List<ProcessingFile>();
            foreach (var f in arr.EnumerateArray())
            {
                ImportFiles.Add(new ProcessingFile
                {
                    Id          = f.GetProperty("id").GetInt32(),
                    FileName    = f.GetProperty("fileName").GetString() ?? "",
                    FileType    = f.GetProperty("fileType").GetString() ?? "",
                    Competence  = f.TryGetProperty("competence", out var c) ? c.GetString() ?? "" : "",
                    Status      = f.GetProperty("status").GetString() ?? "PENDING",
                    CreatedAt   = f.GetProperty("uploadedAt").GetDateTime()
                });
            }
        }
        catch (Exception ex) { _logger.LogError("Erro ao carregar arquivos: {Msg}", ex.Message); }
    }

    private async Task LoadSummary(string competence)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token)) return;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.GetAsync($"/api/processing/closing-summary?competence={competence}");
            if (!resp.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Summary = new ClosingSummaryDto
            {
                Competence        = root.GetProperty("competence").GetString() ?? competence,
                TotalReceitas     = root.GetProperty("receitas").GetProperty("total").GetDecimal(),
                TotalDespesas     = root.GetProperty("despesas").GetProperty("total").GetDecimal(),
                TotalFolhaBruto   = root.GetProperty("folhaPagamento").GetProperty("totalBruto").GetDecimal(),
                TotalFolhaLiquido = root.GetProperty("folhaPagamento").GetProperty("totalLiquido").GetDecimal(),
                Funcionarios      = root.GetProperty("folhaPagamento").GetProperty("funcionarios").GetInt32(),
                TotalTransf       = root.GetProperty("transferencias").GetProperty("total").GetDecimal(),
                TotalEmitidas     = root.GetProperty("duplicatasEmitidas").GetProperty("total").GetDecimal(),
                Resultado         = root.GetProperty("resultado").GetDecimal(),
                ResultadoLabel    = root.GetProperty("resultadoLabel").GetString() ?? ""
            };

            // Detalhes de receitas
            Summary.DetalhesReceitas = new List<DetalheTipo>();
            foreach (var det in root.GetProperty("receitas").GetProperty("detalhes").EnumerateArray())
                Summary.DetalhesReceitas.Add(new DetalheTipo
                { Tipo = det.GetProperty("tipo").GetString() ?? "", Total = det.GetProperty("total").GetDecimal(), Count = det.GetProperty("count").GetInt32() });

            // Detalhes de despesas
            Summary.DetalhesDespesas = new List<DetalheTipo>();
            foreach (var det in root.GetProperty("despesas").GetProperty("detalhes").EnumerateArray())
                Summary.DetalhesDespesas.Add(new DetalheTipo
                { Tipo = det.GetProperty("tipo").GetString() ?? "", Total = det.GetProperty("total").GetDecimal(), Count = det.GetProperty("count").GetInt32() });
        }
        catch (Exception ex) { _logger.LogError("Erro ao carregar resumo: {Msg}", ex.Message); }
    }

    private async Task LoadTransactions(string competence)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token)) return;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.GetAsync($"/api/processing/transactions?competence={competence}&limit=100");
            if (!resp.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("transactions", out var arr)) return;

            Transactions = new List<TransactionDto>();
            foreach (var t in arr.EnumerateArray())
            {
                Transactions.Add(new TransactionDto
                {
                    Id              = t.GetProperty("id").GetInt32(),
                    TransactionType = t.GetProperty("transactionType").GetString() ?? "",
                    Documento       = t.GetProperty("documento").GetString() ?? "",
                    Counterpart     = t.TryGetProperty("counterpart", out var cp) ? cp.GetString() ?? "" : "",
                    Valor           = t.GetProperty("valor").GetDecimal(),
                    DataTransacao   = t.GetProperty("dataTransacao").GetDateTime(),
                    Categoria       = t.TryGetProperty("categoria", out var cat) && cat.ValueKind != JsonValueKind.Null ? cat.GetString() ?? "" : "",
                    Status          = t.GetProperty("status").GetString() ?? ""
                });
            }
        }
        catch (Exception ex) { _logger.LogError("Erro ao carregar transações: {Msg}", ex.Message); }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public class ProcessingFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public string Competence { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ClosingSummaryDto
{
    public string Competence { get; set; } = "";
    public decimal TotalReceitas { get; set; }
    public decimal TotalDespesas { get; set; }
    public decimal TotalFolhaBruto { get; set; }
    public decimal TotalFolhaLiquido { get; set; }
    public int Funcionarios { get; set; }
    public decimal TotalTransf { get; set; }
    public decimal TotalEmitidas { get; set; }
    public decimal Resultado { get; set; }
    public string ResultadoLabel { get; set; } = "";
    public List<DetalheTipo> DetalhesReceitas { get; set; } = new();
    public List<DetalheTipo> DetalhesDespesas { get; set; } = new();
}

public class DetalheTipo
{
    public string Tipo { get; set; } = "";
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class TransactionDto
{
    public int Id { get; set; }
    public string TransactionType { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Counterpart { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime DataTransacao { get; set; }
    public string Categoria { get; set; } = "";
    public string Status { get; set; } = "";
}
