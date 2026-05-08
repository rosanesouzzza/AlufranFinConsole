using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AlufranFinConsole.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Invalid credentials" });

        var token = GenerateJwtToken(user);
        return Ok(new { token, userId = user.Id, email = user.Email });
    }

    /// <summary>Temporary: shows JWT config and validates token from Authorization header. Remove after debugging.</summary>
    [HttpGet("debug")]
    [AllowAnonymous]
    public IActionResult Debug()
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var keyValue = jwtSettings["Key"] ?? "(null)";
        var config = new
        {
            issuer = jwtSettings["Issuer"],
            audience = jwtSettings["Audience"],
            expireMinutes = jwtSettings["ExpireMinutes"],
            keyLength = keyValue.Length,
            keyFirst8 = keyValue.Length >= 8 ? keyValue.Substring(0, 8) + "..." : keyValue,
            env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            jwtEnvKey = System.Environment.GetEnvironmentVariable("Jwt__Key") is { } envKey && envKey.Length >= 8
                ? envKey.Substring(0, 8) + "..."
                : "(not set)"
        };

        // Try to validate the token from the Authorization header directly
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var keyBytes = Encoding.ASCII.GetBytes(keyValue);
                var validParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
                handler.ValidateToken(token, validParams, out var validated);
                var claims = (validated as JwtSecurityToken)?.Claims.Select(c => new { c.Type, c.Value });
                return Ok(new { config, tokenPresent = true, tokenValid = true, claims });
            }
            catch (Exception ex)
            {
                return Ok(new { config, tokenPresent = true, tokenValid = false, error = ex.Message });
            }
        }

        // Diagnostic: show raw Authorization values so we can inspect proxy injection
        var rawAuthValues = Request.Headers["Authorization"].ToArray();
        var authDiag = rawAuthValues.Select((v, i) => new
        {
            index = i,
            length = v?.Length ?? 0,
            dotCount = v?.Count(c => c == '.') ?? 0,
            first80 = (v?.Length ?? 0) > 80 ? v![..80] + "…" : v
        });
        return Ok(new { config, tokenPresent = false, hint = "Pass Authorization: Bearer <token> header to validate", authDiag });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password required" });

        var user = new IdentityUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "Registration failed", errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "User registered successfully", userId = user.Id, email = user.Email });
    }

    private string GenerateJwtToken(IdentityUser user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSettings["Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.UserName)
        };

        var expireMinutes = int.TryParse(jwtSettings["ExpireMinutes"], out var mins) ? mins : 480;
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
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
}
