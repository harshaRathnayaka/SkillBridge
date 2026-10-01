using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SkillBridge.ApiService.Auth;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Courses;
using SkillBridge.ApiService.Dashboard;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Data.Seed;
using SkillBridge.ApiService.Email;
using SkillBridge.ApiService.Jobs;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SkillBridgeDb")));

// Keeps password-reset/email-confirmation tokens valid across container restarts — without
// this, ASP.NET Core's default ephemeral key ring is regenerated on every redeploy and every
// outstanding token silently stops validating. Stored in the same Postgres database as
// everything else rather than a local file/volume, since the compute host (SnapDeploy's free
// tier, like every other card-free host) has no persistent disk at all.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Account lockout after repeated failed logins — closes the brute-force gap the
        // stress tests deliberately don't cover (they only check the endpoint survives a
        // burst of bad attempts, not that anything stops someone from trying forever).
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Password reset codes expire in 1 hour rather than Identity's 1-day default.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();

// First endpoint that needs to know who the caller is (change-password) — validates the same
// JWTs TokenService issues. MapInboundClaims is off so claim types stay exactly as issued
// ("sub" stays "sub") instead of Identity's legacy WS-Federation-style remapping.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });
builder.Services.AddAuthorization();

// Per-IP rate limit on the whole /api/auth surface — the "someone scripts thousands of
// requests" threat that account lockout doesn't cover (lockout is per-account; this is
// per-source). Values are configurable so tests can use a generous limit by default (see
// ApiServiceTestFactory) and a tiny one in the one test that specifically proves 429 works.
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(["Too many requests. Please try again later."]), cancellationToken);
    };

    var permitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 20);
    var windowSeconds = builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);

    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapDashboardEndpoints();
app.MapCourseEndpoints();
app.MapJobEndpoints();

// Deliberately dependency-free (no DB check) and always mapped, unlike
// SkillBridge.ServiceDefaults' own /health (Development-only, by design — see its comment on
// why exposing detailed health-check results in production has security implications). This is
// just a plain "the process is up" signal for whatever host's liveness probe needs one
// (SnapDeploy, or anything else) — it doesn't reveal anything about the database or its
// connection state.
app.MapGet("/health", () => Results.Ok());

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Production (Npgsql) always runs the real, committed migrations. ApiServiceTestFactory
    // swaps in a SQLite connection for speed — SQLite can't apply Postgres-shaped migration DDL,
    // so tests instead build the schema directly from the current model.
    if (db.Database.IsNpgsql())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    await RoleSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Not used at all, on any host: every PaaS this app has run on (Fly.io, Render, SnapDeploy)
// terminates TLS at the edge and forwards plain HTTP internally, so this middleware would just
// redirect-loop against itself if it ever ran — observed in practice on SnapDeploy, where it
// fired despite the container being configured for Production. Gating it on
// Environment.IsProduction() assumes that env var is reliably honored by the host, which isn't
// true everywhere; simplest fix is to not depend on it being right.

app.Run();

public partial class Program;
