using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AlufranFinConsole.Infrastructure.Persistence.ApplicationDbContext>(options =>
{
    if (builder.Environment.IsProduction())
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// AddIdentityCore does NOT register cookie auth as the default scheme,
// so JWT Bearer can be the sole default authenticator for this API.
builder.Services
    .AddIdentityCore<Microsoft.AspNetCore.Identity.IdentityUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<AlufranFinConsole.Infrastructure.Persistence.ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var key = System.Text.Encoding.ASCII.GetBytes(secretKey);

builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        // Events: pick OUR JWT when Cloudflare injects its own JWT in the same
        // Authorization header (Cloudflare's token is valid JWS but has no exp/iss).
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Primary path: X-Auth-Token header — Cloudflare never touches custom headers,
                // so our JWT arrives here intact even behind Render's Cloudflare proxy.
                var customToken = context.Request.Headers["X-Auth-Token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(customToken))
                {
                    var extracted = customToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? customToken["Bearer ".Length..].Trim()
                        : customToken.Trim();
                    context.Token = extracted;
                    // Diagnostic headers — remove after debugging
                    try
                    {
                        context.Response.Headers["X-Tok-Src"] = "X-Auth-Token";
                        context.Response.Headers["X-Tok-Len"] = extracted.Length.ToString();
                        context.Response.Headers["X-Tok-Dots"] = extracted.Count(c => c == '.').ToString();
                        context.Response.Headers["X-Tok-P30"] = extracted.Length >= 30 ? extracted[..30] : extracted;
                    }
                    catch { }
                    return Task.CompletedTask;
                }

                // Fallback: attempt to extract our JWT from the Authorization header.
                // Useful for direct calls (curl, Swagger, local dev) that don't go through Cloudflare.
                // Cloudflare replaces Authorization with its own token, so this path may fail on Render.
                var authHeaderStr = context.Request.Headers["Authorization"].ToString();

                // Find every Bearer token that matches the JWS pattern eyJ<hdr>.<payload>.<sig>
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    authHeaderStr,
                    @"Bearer\s+(eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var candidate = m.Groups[1].Value;
                    var parts = candidate.Split('.');
                    if (parts.Length != 3) continue;

                    try
                    {
                        // Quick base64url-decode the payload to check the issuer.
                        // Our tokens always carry iss:AlufranConsole; Cloudflare's don't.
                        var padded = parts[1].Replace('-', '+').Replace('_', '/')
                            .PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                        var payloadJson = System.Text.Encoding.UTF8.GetString(
                            Convert.FromBase64String(padded));

                        if (payloadJson.Contains("\"iss\":\"AlufranConsole\""))
                        {
                            context.Token = candidate;
                            return Task.CompletedTask;
                        }
                    }
                    catch { /* malformed token – skip */ }
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                try
                {
                    var msg = (context.Exception?.Message ?? "unknown error")
                        .Replace('\r', ' ').Replace('\n', ' ');
                    context.Response.Headers["X-Auth-Error"] =
                        msg.Length > 200 ? msg[..200] : msg;
                }
                catch { /* never throw from this handler */ }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IFileUploadService, AlufranFinConsole.Application.Services.FileUploadService>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global error handler: turns empty 500 responses into JSON with the actual error detail
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "Internal server error",
            detail = ex?.Message,
            type = ex?.GetType().Name
        });
    });
});

// UseHttpsRedirection removed: Render terminates TLS at its proxy layer;
// the internal connection is plain HTTP and a redirect here would strip the
// Authorization header on any HTTPS→HTTP redirect cycle.
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AlufranFinConsole.Infrastructure.Persistence.ApplicationDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
    if (!userManager.Users.Any())
    {
        var admin = new Microsoft.AspNetCore.Identity.IdentityUser { UserName = "admin@alufran.local", Email = "admin@alufran.local" };
        userManager.CreateAsync(admin, "AlufranAdmin@2026").GetAwaiter().GetResult();
    }
}

app.Run();
