using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class DreModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DreModel> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    // ── Propriedades de exibição ──────────────────────────────────────────

    public bool IsAuthenticated { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Competence { get; set; }

    public List<string> CompetenceOptions { get; set; } = new();
    public DreDto? Dre { get; set; }
    public ClosingStatusDto? Status { get; set; }

    [BindProperty] public string? ApproveNotes { get; set; }

    public DreModel(IHttpClientFactory httpClientFactory, ILogger<DreModel> logger,
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

        if (!IsAuthenticated) return;

        await LoadCompetenceOptions();

        if (string.IsNullOrEmpty(Competence) && CompetenceOptions.Any())
            Competence = CompetenceOptions.First();

        if (!string.IsNullOrEmpty(Competence))
        {
            await LoadDre(Competence);
            await LoadStatus(Competence);
        }
    }

    public async Task<IActionResult> OnPostApproveAsync()
    {
        await EnsureAuth();
        if (!IsAuthenticated || string.IsNullOrEmpty(Competence)) return Page();

        var token = HttpContext.Session.GetString("AuthToken")!;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent(
            JsonSerializer.Serialize(new { notes = ApproveNotes ?? "" }),
            System.Text.Encoding.UTF8, "application/json");
        var resp = await _httpClient.PostAsync($"/api/processing/approve?competence={Competence}", body);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            ErrorMessage = $"Erro ao aprovar: {err}";
        }

        return RedirectToPage(new { competence = Competence });
    }

    public async Task<IActionResult> OnPostReopenAsync()
    {
        await EnsureAuth();
        if (!IsAuthenticated || string.IsNullOrEmpty(Competence)) return Page();

        var token = HttpContext.Session.GetString("AuthToken")!;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/processing/approve?competence={Competence}");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { notes = ApproveNotes ?? "" }),
            System.Text.Encoding.UTF8, "application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _httpClient.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            ErrorMessage = $"Erro ao reabrir: {err}";
        }

        return RedirectToPage(new { competence = Competence });
    }

    private async Task EnsureAuth()
    {
        CheckAuthentication();
        if (!IsAuthenticated && _env.IsDevelopment())
            await TryAutoLoginAsync();
        await LoadCompetenceOptions();
        if (!string.IsNullOrEmpty(Competence))
        {
            await LoadDre(Competence);
            await LoadStatus(Competence);
        }
    }

    // ── Carregamento de dados ─────────────────────────────────────────────

    private async Task LoadCompetenceOptions()
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken")!;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.GetAsync("/api/upload?limit=500");
            if (!resp.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("files", out var arr)) return;

            var competences = new HashSet<string>();
            foreach (var f in arr.EnumerateArray())
            {
                if (f.TryGetProperty("competence", out var c) && !string.IsNullOrEmpty(c.GetString()))
                    competences.Add(c.GetString()!);
            }
            CompetenceOptions = competences.OrderByDescending(x => x).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao carregar competências: {Msg}", ex.Message);
        }
    }

    private async Task LoadDre(string competence)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken")!;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.GetAsync($"/api/processing/dre?competence={competence}");
            if (!resp.IsSuccessStatusCode)
            {
                ErrorMessage = $"Erro ao carregar DRE: {resp.StatusCode}";
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Dre = new DreDto
            {
                Competence   = root.GetProperty("competence").GetString() ?? "",
                GeradoEm     = root.GetProperty("geradoEm").GetDateTime(),

                ReceitaBruta     = GetLinha(root, "receitaBruta"),
                Deducoes         = GetLinha(root, "deducoes"),
                ReceitaLiquida   = GetLinhaSingle(root, "receitaLiquida"),
                CustoServicos    = GetLinha(root, "custoServicos"),
                LucroBruto       = GetLinhaSingle(root, "lucroBruto"),
                DespesasOp       = GetLinha(root, "despesasOperacionais"),
                DespesasPessoal  = GetFopagLinha(root),
                ResultadoOp      = GetLinhaComLabel(root, "resultadoOperacional"),
                Transferencias   = GetTransf(root),
                ResultadoLiquido = GetLinhaComLabel(root, "resultadoLiquido"),

                FuncionarioDetalhe = GetFuncionarios(root)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao carregar DRE: {Msg}", ex.Message);
            ErrorMessage = "Erro ao carregar DRE.";
        }
    }

    // ── Helpers de mapeamento JSON → DTO ─────────────────────────────────

    private static LinhaGrupo GetLinha(JsonElement root, string prop)
    {
        var g = root.GetProperty(prop);
        var linha = new LinhaGrupo
        {
            Valor  = g.GetProperty("valor").GetDecimal(),
            Margem = g.GetProperty("margem").GetDecimal()
        };
        if (g.TryGetProperty("itens", out var itens))
        {
            foreach (var i in itens.EnumerateArray())
            {
                linha.Itens.Add(new LinhaItem
                {
                    Codigo    = i.GetProperty("codigo").GetString() ?? "",
                    Descricao = i.GetProperty("descricao").GetString() ?? "",
                    Valor     = i.GetProperty("valor").GetDecimal(),
                    Qtd       = i.GetProperty("quantidade").GetInt32()
                });
            }
        }
        return linha;
    }

    private static LinhaGrupo GetLinhaSingle(JsonElement root, string prop)
    {
        var g = root.GetProperty(prop);
        return new LinhaGrupo
        {
            Valor  = g.GetProperty("valor").GetDecimal(),
            Margem = g.GetProperty("margem").GetDecimal()
        };
    }

    private static LinhaComLabel GetLinhaComLabel(JsonElement root, string prop)
    {
        var g = root.GetProperty(prop);
        return new LinhaComLabel
        {
            Valor  = g.GetProperty("valor").GetDecimal(),
            Margem = g.GetProperty("margem").GetDecimal(),
            Label  = g.GetProperty("label").GetString() ?? ""
        };
    }

    private static LinhaGrupo GetFopagLinha(JsonElement root)
    {
        var g = root.GetProperty("despesasPessoal");
        var linha = new LinhaGrupo
        {
            Valor  = g.GetProperty("valorLiquido").GetDecimal(),
            Margem = g.GetProperty("margem").GetDecimal()
        };
        if (g.TryGetProperty("itens", out var itens))
        {
            foreach (var i in itens.EnumerateArray())
            {
                linha.Itens.Add(new LinhaItem
                {
                    Codigo    = i.GetProperty("codigo").GetString() ?? "",
                    Descricao = i.GetProperty("descricao").GetString() ?? "",
                    Valor     = i.GetProperty("valor").GetDecimal(),
                    Qtd       = i.GetProperty("quantidade").GetInt32()
                });
            }
        }
        return linha;
    }

    private static TransfInfo GetTransf(JsonElement root)
    {
        var g = root.GetProperty("transferencias");
        return new TransfInfo
        {
            Valor = g.GetProperty("valor").GetDecimal(),
            Qtd   = g.GetProperty("quantidade").GetInt32(),
            Nota  = g.GetProperty("nota").GetString() ?? ""
        };
    }

    private static List<FuncionarioRow> GetFuncionarios(JsonElement root)
    {
        var list = new List<FuncionarioRow>();
        if (!root.TryGetProperty("despesasPessoal", out var dp)) return list;
        if (!dp.TryGetProperty("detalhe", out var det)) return list;
        foreach (var f in det.EnumerateArray())
        {
            list.Add(new FuncionarioRow
            {
                Matricula    = f.GetProperty("matricula").GetString() ?? "",
                Funcionario  = f.GetProperty("funcionario").GetString() ?? "",
                Cargo        = f.TryGetProperty("cargo", out var c) ? c.GetString() ?? "" : "",
                ValorBruto   = f.GetProperty("valorBruto").GetDecimal(),
                Descontos    = f.GetProperty("descontos").GetDecimal(),
                ValorLiquido = f.GetProperty("valorLiquido").GetDecimal()
            });
        }
        return list;
    }

    private async Task LoadStatus(string competence)
    {
        try
        {
            var token = HttpContext.Session.GetString("AuthToken")!;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _httpClient.GetAsync($"/api/processing/status?competence={competence}");
            if (!resp.IsSuccessStatusCode) return;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Status = new ClosingStatusDto
            {
                IsAprovado    = root.GetProperty("isAprovado").GetBoolean(),
                PodeAprovar   = root.GetProperty("podeAprovar").GetBoolean(),
                StatusGeral   = root.GetProperty("statusGeral").GetString() ?? "",
                PipelineOk    = root.GetProperty("pipeline").GetProperty("pipelineOk").GetBoolean(),
                TotalFiles    = root.GetProperty("pipeline").GetProperty("totalFiles").GetInt32(),
                CompletedFiles= root.GetProperty("pipeline").GetProperty("completedFiles").GetInt32()
            };
            if (root.TryGetProperty("aprovacao", out var apr) && apr.ValueKind != JsonValueKind.Null)
            {
                Status.ApprovedBy = apr.TryGetProperty("approvedBy", out var ab) ? ab.GetString() : null;
                Status.ApprovedAt = apr.TryGetProperty("approvedAt", out var aa) ? aa.GetDateTime() : null;
                Status.Notes      = apr.TryGetProperty("notes", out var n) && n.ValueKind != JsonValueKind.Null
                    ? n.GetString() : null;
            }
        }
        catch (Exception ex) { _logger.LogError("Erro ao carregar status: {Msg}", ex.Message); }
    }

    // ── Auth helpers ──────────────────────────────────────────────────────

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

public class DreDto
{
    public string   Competence   { get; set; } = "";
    public DateTime GeradoEm     { get; set; }

    public LinhaGrupo    ReceitaBruta    { get; set; } = new();
    public LinhaGrupo    Deducoes        { get; set; } = new();
    public LinhaGrupo    ReceitaLiquida  { get; set; } = new();
    public LinhaGrupo    CustoServicos   { get; set; } = new();
    public LinhaGrupo    LucroBruto      { get; set; } = new();
    public LinhaGrupo    DespesasOp      { get; set; } = new();
    public LinhaGrupo    DespesasPessoal { get; set; } = new();
    public LinhaComLabel ResultadoOp     { get; set; } = new();
    public TransfInfo    Transferencias  { get; set; } = new();
    public LinhaComLabel ResultadoLiquido{ get; set; } = new();

    public List<FuncionarioRow> FuncionarioDetalhe { get; set; } = new();

    public string CompetenceDisplay => DateTime.TryParseExact(Competence + "-01",
        "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d)
        ? d.ToString("MMMM/yyyy", new System.Globalization.CultureInfo("pt-BR")).ToUpper()
        : Competence;
}

public class LinhaGrupo
{
    public decimal        Valor  { get; set; }
    public decimal        Margem { get; set; }
    public List<LinhaItem> Itens { get; set; } = new();
}

public class LinhaComLabel
{
    public decimal Valor  { get; set; }
    public decimal Margem { get; set; }
    public string  Label  { get; set; } = "";
}

public class LinhaItem
{
    public string  Codigo    { get; set; } = "";
    public string  Descricao { get; set; } = "";
    public decimal Valor     { get; set; }
    public int     Qtd       { get; set; }
}

public class TransfInfo
{
    public decimal Valor { get; set; }
    public int     Qtd   { get; set; }
    public string  Nota  { get; set; } = "";
}

public class ClosingStatusDto
{
    public bool     IsAprovado     { get; set; }
    public bool     PodeAprovar    { get; set; }
    public string   StatusGeral    { get; set; } = "";
    public bool     PipelineOk     { get; set; }
    public int      TotalFiles     { get; set; }
    public int      CompletedFiles { get; set; }
    public string?  ApprovedBy     { get; set; }
    public DateTime? ApprovedAt    { get; set; }
    public string?  Notes          { get; set; }
}

public class FuncionarioRow
{
    public string  Matricula    { get; set; } = "";
    public string  Funcionario  { get; set; } = "";
    public string  Cargo        { get; set; } = "";
    public decimal ValorBruto   { get; set; }
    public decimal Descontos    { get; set; }
    public decimal ValorLiquido { get; set; }
}
