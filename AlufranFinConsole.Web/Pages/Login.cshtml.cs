using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace AlufranFinConsole.Web.Pages;

public class LoginModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; set; }

    public LoginModel(IHttpClientFactory httpClientFactory, ILogger<LoginModel> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _logger = logger;
    }

    public void OnGet()
    {
        // Verificar se já está autenticado
        var token = HttpContext.Session.GetString("AuthToken");
        if (!string.IsNullOrEmpty(token))
        {
            RedirectToPage("Staging");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Email e senha são obrigatórios.";
            return Page();
        }

        try
        {
            // Fazer login na API
            var loginRequest = new { email = Email, password = Password };
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

                    // Armazenar token na sessão
                    HttpContext.Session.SetString("AuthToken", token!);
                    HttpContext.Session.SetString("UserEmail", Email);

                    _logger.LogInformation($"Usuário {Email} autenticado com sucesso.");

                    // Redirecionar para Staging
                    return RedirectToPage("Staging");
                }
                else
                {
                    ErrorMessage = "Resposta de autenticação inválida.";
                    return Page();
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Email ou senha inválidos.";
                _logger.LogWarning($"Tentativa de login falhou para {Email}.");
                return Page();
            }
            else
            {
                ErrorMessage = $"Erro na autenticação: {response.StatusCode}";
                _logger.LogError($"Erro ao autenticar: {response.StatusCode}");
                return Page();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao conectar com o servidor: {ex.Message}";
            _logger.LogError($"Erro na autenticação: {ex.Message}");
            return Page();
        }
    }
}
