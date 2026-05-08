using Microsoft.AspNetCore.Mvc;
using AlufranFinConsole.Infrastructure.Persistence;
using AlufranFinConsole.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace AlufranFinConsole.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISession _session;

    public AuthController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _session = httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Session required");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        _session.SetString("UserId", user.Id.ToString());
        _session.SetString("Email", user.Email);

        return Ok(new { message = "Logged in successfully", userId = user.Id, email = user.Email });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var existingUser = _context.Users.FirstOrDefault(u => u.Email == request.Email);
        if (existingUser != null)
            return BadRequest(new { error = "Email already registered" });

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName ?? "User",
            PasswordHash = HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _session.SetString("UserId", user.Id.ToString());
        _session.SetString("Email", user.Email);

        return Ok(new { message = "User registered successfully", userId = user.Id, email = user.Email });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _session.Clear();
        return Ok(new { message = "Logged out successfully" });
    }

    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    private bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }
}

public class LoginRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string? FullName { get; set; }
}
