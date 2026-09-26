using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Email;

namespace SkillBridge.ApiService.Tests.TestSupport;

// Boots the real Program.cs pipeline (Identity, JWT auth, role seeding, schema creation)
// against a private in-memory SQLite database instead of the dev appsettings.json file/a real
// Postgres server. Production runs on Npgsql with real committed migrations; Program.cs detects
// this override (Database.IsNpgsql() is false) and calls EnsureCreatedAsync() instead of
// MigrateAsync() — Postgres-shaped migration DDL isn't valid SQLite, so tests build the schema
// straight from the current model instead of replaying migrations. Migrations themselves are
// exercised for real by the actual deploy, not by this suite.
//
// Uses SQLite's shared-cache in-memory mode (a named db shared by connection string) rather
// than handing every DbContext the same single SqliteConnection object: a plain SqliteConnection
// isn't safe to use from multiple threads at once, which would make concurrent-refresh tests
// (deliberately firing two requests at the same time) flaky for the wrong reason. Each request
// gets its own connection to the same shared in-memory database, so genuine DB-level locking
// behavior is exercised, same as a real SQLite file would.
public sealed class ApiServiceTestFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly int _rateLimitPermitLimit;
    private SqliteConnection? _keepAliveConnection;

    public RecordingEmailSender EmailSender { get; } = new();

    // The default limit is deliberately generous so existing correctness/stress tests (which
    // fire far more than a real single client would in a minute) aren't throttled — only
    // RateLimitingTests exercises a real, tight limit to prove 429s actually happen.
    public ApiServiceTestFactory(int rateLimitPermitLimit = 10_000)
    {
        _rateLimitPermitLimit = rateLimitPermitLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Keeps the shared in-memory database alive for the factory's lifetime — SQLite
        // drops an in-memory db as soon as its last connection closes.
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthPermitLimit"] = _rateLimitPermitLimit.ToString(),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            // RemoveAll<DbContextOptions<T>> alone isn't enough once production registers a
            // different provider (Npgsql): EF Core also tracks each AddDbContext call's own
            // configuration action via this separate service type, and having both providers'
            // configuration actions present throws "Only a single database provider can be
            // registered" when the options are built.
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connectionString));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection?.Dispose();
        }
    }
}
