using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AlufranFinConsole.Infrastructure.Persistence;

namespace AlufranFinConsole.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISession _session;

    public AuthController(UserManager<IdentityUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _session = httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Session required");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Invalid credentials" });

        _session.SetString("UserId", user.Id);
        _session.SetString("Email", user.Email);

        return Ok(new { message = "Logged in successfully", userId = user.Id, email = user.Email });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest(new { error = "Email already registered" });

        var user = new IdentityUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "Registration failed", errors = result.Errors.Select(e => e.Description) });

        _session.SetString("UserId", user.Id);
        _session.SetString("Email", user.Email);

        return Ok(new { message = "User registered successfully", userId = user.Id, email = user.Email });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _session.Clear();
        return Ok(new { message = "Logged out successfully" });
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
