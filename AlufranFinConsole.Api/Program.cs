using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usePostgres = builder.Configuration.GetValue<bool>("Database:UsePostgres");

builder.Services.AddDbContext<AlufranFinConsole.Infrastructure.Persistence.ApplicationDbContext>(options =>
{
    if (usePostgres || builder.Environment.IsProduction())
    {
        options.UseNpgsql(connectionString,
            npg => npg.MigrationsAssembly("AlufranFinConsole.Infrastructure"));
    }
    else
    {
        options.UseSqlite(connectionString,
            sqlite => sqlite.MigrationsAssembly("AlufranFinConsole.Infrastructure"));
    }
});

// IApplicationDbContext — expõe o DbContext como interface para Application Services
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IApplicationDbContext>(
    sp => sp.GetRequiredService<AlufranFinConsole.Infrastructure.Persistence.ApplicationDbContext>());

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
        // UseSecurityTokenValidators = true forces JwtSecurityTokenHandler (the pre-.NET 7 validator)
        // instead of JsonWebTokenHandler. JwtSecurityTokenHandler correctly honours context.Token
        // set in OnMessageReceived, while JsonWebTokenHandler re-reads from the Authorization header
        // (which Cloudflare replaces on Render free tier).
        options.UseSecurityTokenValidators = true;

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

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            // Read from X-Auth-Token first (Cloudflare doesn't modify custom headers).
            // JwtSecurityTokenHandler respects context.Token set here, unlike JsonWebTokenHandler.
            OnMessageReceived = context =>
            {
                var xat = context.Request.Headers["X-Auth-Token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(xat))
                {
                    context.Token = xat.Trim();
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

// ── Application Services (Fase 7 — Pipeline de Saneamento) ─────────────────
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IFileUploadService,              AlufranFinConsole.Application.Services.FileUploadService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.ITextNormalizationService,       AlufranFinConsole.Application.Services.TextNormalizationService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IDataValidationService,          AlufranFinConsole.Application.Services.DataValidationService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IColumnMappingService,           AlufranFinConsole.Application.Services.ColumnMappingService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IDiscardService,                 AlufranFinConsole.Application.Services.DiscardService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IQaIssueService,                 AlufranFinConsole.Application.Services.QaIssueService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IClassificationService,          AlufranFinConsole.Application.Services.ClassificationService>();
builder.Services.AddScoped<AlufranFinConsole.Application.Services.IFinancialSanitizationService,   AlufranFinConsole.Application.Services.FinancialSanitizationService>();

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

// X-Auth-Token bridge: copy X-Auth-Token → Authorization: Bearer before the JWT middleware.
// Cloudflare (Render free tier) replaces the Authorization header with its own JWT, but does
// NOT touch custom headers, so clients send X-Auth-Token: <jwt> instead.
// Moving it here at the server side ensures the standard JWT Bearer pipeline sees our token.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Headers.TryGetValue("X-Auth-Token", out var xat) &&
        !string.IsNullOrWhiteSpace(xat.ToString()) &&
        !ctx.Request.Headers.ContainsKey("Authorization"))
    {
        var raw = xat.ToString().Trim();
        ctx.Request.Headers["Authorization"] = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? raw
            : "Bearer " + raw;
    }
    await next();
});

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

    // Seed default ColumnMappings (idempotent — skips if table already has rows)
    var mappingSvc = scope.ServiceProvider
        .GetRequiredService<AlufranFinConsole.Application.Services.IColumnMappingService>();
    mappingSvc.SeedDefaultMappingsAsync().GetAwaiter().GetResult();
}

app.Run();
