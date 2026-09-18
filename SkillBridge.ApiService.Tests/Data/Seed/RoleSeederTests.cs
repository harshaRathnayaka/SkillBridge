using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Data.Seed;

namespace SkillBridge.ApiService.Tests.Data.Seed;

public class RoleSeederTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite("DataSource=:memory:"));
        services.AddLogging();
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_from_empty_database_creates_exactly_the_four_roles()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        await RoleSeeder.SeedAsync(scope.ServiceProvider);

        var roleNames = await db.Roles.Select(r => r.Name).ToListAsync();
        Assert.Equal(
            new[] { "Teacher", "Student", "JobSeeker", "JobGiver" }.OrderBy(n => n),
            roleNames.OrderBy(n => n));
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_when_run_twice()
    {
        using var provider = BuildServices();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        await RoleSeeder.SeedAsync(scope.ServiceProvider);
        await RoleSeeder.SeedAsync(scope.ServiceProvider);

        var roleCount = await db.Roles.CountAsync();
        Assert.Equal(4, roleCount);
    }
}
