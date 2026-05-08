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
        // Expose the exact validation error as a response header for diagnostics
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.Response.Headers["X-Auth-Error"] =
                    context.Exception.GetType().Name + ": " + context.Exception.Message[..Math.Min(200, context.Exception.Message.Length)];
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
