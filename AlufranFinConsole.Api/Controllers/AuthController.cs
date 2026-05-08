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

    /// <summary>Temporary: shows JWT config, all received headers and validates token. Remove after debugging.</summary>
    [HttpGet("debug")]
    [AllowAnonymous]
    public IActionResult Debug()
    {
        const string version = "v3-xauth";

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

        // Show ALL incoming headers so we can see what Cloudflare/Render passes through
        var receivedHeaders = Request.Headers
            .Where(h => !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            .Select(h => new
            {
                name = h.Key,
                count = h.Value.Count,
                values = h.Value.Select((v, i) => new
                {
                    i,
                    len = v?.Length ?? 0,
                    dots = v?.Count(c => c == '.') ?? 0,
                    val = (v?.Length ?? 0) > 120 ? v![..120] + "…" : v
                })
            });

        // Try to validate the token from Authorization header directly
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
                return Ok(new { version, config, tokenPresent = true, tokenValid = true, claims, receivedHeaders });
            }
            catch (Exception ex)
            {
                return Ok(new { version, config, tokenPresent = true, tokenValid = false, error = ex.Message, receivedHeaders });
            }
        }

        // Also try X-Auth-Token directly
        var xAuthToken = Request.Headers["X-Auth-Token"].FirstOrDefault();
        string? xAuthResult = null;
        if (!string.IsNullOrWhiteSpace(xAuthToken))
        {
            var rawTok = xAuthToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? xAuthToken["Bearer ".Length..].Trim()
                : xAuthToken.Trim();
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
                handler.ValidateToken(rawTok, validParams, out _);
                xAuthResult = "VALID";
            }
            catch (Exception ex)
            {
                xAuthResult = "INVALID: " + ex.Message;
            }
        }

        return Ok(new { version, config, tokenPresent = false, xAuthTokenPresent = !string.IsNullOrWhiteSpace(xAuthToken), xAuthResult, receivedHeaders });
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
