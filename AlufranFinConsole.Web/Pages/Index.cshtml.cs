using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class IndexModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IndexModel> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public bool IsAuthenticated { get; set; }
    public List<CompetenceCard> CompetenceCards { get; set; } = new();
    public List<PipelineFile> PipelineFiles { get; set; } = new();
    public DashboardKpis Kpis { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger,
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
        if (!IsAuthenticated && _env.IsDevelopment())
            await TryAutoLoginAsync();

        if (IsAuthenticated)
            await LoadDashboardData();
    }

    // ── Carregamento de dados ─────────────────────────────────────────────

    private async Task LoadDashboardData()
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken")!;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Todos os arquivos de importação
            var filesResp = await _httpClient.GetAsync("/api/upload?limit=500");
            if (!filesResp.IsSuccessStatusCode) return;

            using var filesDoc = JsonDocument.Parse(await filesResp.Content.ReadAsStringAsync());
            if (!filesDoc.RootElement.TryGetProperty("files", out var filesArr)) return;

            var allFiles = new List<PipelineFile>();
            var competences = new HashSet<string>();

            foreach (var f in filesArr.EnumerateArray())
            {
                var comp = f.TryGetProperty("competence", out var c) ? c.GetString() ?? "" : "";
                competences.Add(comp);
                allFiles.Add(new PipelineFile
                {
                    Id         = f.GetProperty("id").GetInt32(),
                    FileName   = f.GetProperty("fileName").GetString() ?? "",
                    FileType   = f.GetProperty("fileType").GetString() ?? "",
                    Competence = comp,
                    Status     = f.GetProperty("status").GetString() ?? "PENDING",
                    UploadedAt = f.GetProperty("uploadedAt").GetDateTime()
                });
            }

            PipelineFiles = allFiles.OrderByDescending(f => f.UploadedAt).ToList();

            // 2. Closing summary por competência
            var cards = new List<CompetenceCard>();
            foreach (var comp in competences.OrderByDescending(x => x))
            {
                var summResp = await _httpClient.GetAsync($"/api/processing/closing-summary?competence={comp}");
                if (!summResp.IsSuccessStatusCode) continue;

                using var summDoc = JsonDocument.Parse(await summResp.Content.ReadAsStringAsync());
                var root = summDoc.RootElement;

                var filesInComp = allFiles.Where(f => f.Competence == comp).ToList();

                // Carrega status de aprovação
                bool isAprov = false; string? apprBy = null; DateTime? apprAt = null;
                var stResp = await _httpClient.GetAsync($"/api/processing/status?competence={comp}");
                if (stResp.IsSuccessStatusCode)
                {
                    using var stDoc = JsonDocument.Parse(await stResp.Content.ReadAsStringAsync());
                    var stRoot = stDoc.RootElement;
                    isAprov = stRoot.GetProperty("isAprovado").GetBoolean();
                    if (isAprov && stRoot.TryGetProperty("aprovacao", out var apr)
                        && apr.ValueKind != JsonValueKind.Null)
                    {
                        apprBy = apr.TryGetProperty("approvedBy", out var ab) ? ab.GetString() : null;
                        apprAt = apr.TryGetProperty("approvedAt", out var aa) ? aa.GetDateTime() : null;
                    }
                }

                cards.Add(new CompetenceCard
                {
                    Competence      = comp,
                    TotalReceitas   = root.GetProperty("receitas").GetProperty("total").GetDecimal(),
                    TotalDespesas   = root.GetProperty("despesas").GetProperty("total").GetDecimal(),
                    Resultado       = root.GetProperty("resultado").GetDecimal(),
                    ResultadoLabel  = root.GetProperty("resultadoLabel").GetString() ?? "",
                    Funcionarios    = root.GetProperty("folhaPagamento").GetProperty("funcionarios").GetInt32(),
                    TotalArquivos   = filesInComp.Count,
                    ArquivosConcluidos = filesInComp.Count(f => f.Status is "COMPLETED" or "COMPLETED_WITH_ERRORS"),
                    ArquivosPendentes  = filesInComp.Count(f => f.Status is "PENDING" or "PROCESSING"),
                    IsAprovado      = isAprov,
                    ApprovedBy      = apprBy,
                    ApprovedAt      = apprAt
                });
            }

            CompetenceCards = cards;

            // 3. KPIs globais
            Kpis = new DashboardKpis
            {
                TotalArquivos     = allFiles.Count,
                ArquivosConcluidos = allFiles.Count(f => f.Status is "COMPLETED" or "COMPLETED_WITH_ERRORS"),
                ArquivosPendentes  = allFiles.Count(f => f.Status is "PENDING" or "PROCESSING"),
                TotalReceitas     = cards.Sum(c => c.TotalReceitas),
                TotalDespesas     = cards.Sum(c => c.TotalDespesas),
                Resultado         = cards.Sum(c => c.Resultado),
                TotalCompetencias = cards.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao carregar dashboard: {Msg}", ex.Message);
            ErrorMessage = "Erro ao carregar dados do dashboard.";
        }
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
            var email    = _config["DevLogin:Email"]    ?? "admin@alufran.local";
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
}

// ── DTOs ─────────────────────────────────────────────────────────────────

public class CompetenceCard
{
    public string  Competence       { get; set; } = "";
    public decimal TotalReceitas    { get; set; }
    public decimal TotalDespesas    { get; set; }
    public decimal Resultado        { get; set; }
    public string  ResultadoLabel   { get; set; } = "";
    public int     Funcionarios     { get; set; }
    public int     TotalArquivos    { get; set; }
    public int     ArquivosConcluidos { get; set; }
    public int     ArquivosPendentes  { get; set; }
    public bool    IsAprovado       { get; set; }
    public string? ApprovedBy       { get; set; }
    public DateTime? ApprovedAt     { get; set; }
    public string  CompetenceDisplay => DateTime.TryParseExact(Competence + "-01",
        "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d)
        ? d.ToString("MMMM/yyyy", new System.Globalization.CultureInfo("pt-BR")).ToUpper()
        : Competence;
}

public class PipelineFile
{
    public int      Id         { get; set; }
    public string   FileName   { get; set; } = "";
    public string   FileType   { get; set; } = "";
    public string   Competence { get; set; } = "";
    public string   Status     { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}

public class DashboardKpis
{
    public int     TotalArquivos      { get; set; }
    public int     ArquivosConcluidos { get; set; }
    public int     ArquivosPendentes  { get; set; }
    public decimal TotalReceitas      { get; set; }
    public decimal TotalDespesas      { get; set; }
    public decimal Resultado          { get; set; }
    public int     TotalCompetencias  { get; set; }
}
