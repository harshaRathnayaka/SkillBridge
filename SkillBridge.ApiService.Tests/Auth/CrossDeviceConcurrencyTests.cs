using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

// Verifies requirement: a user can be logged in on mobile and web at the same time —
// logging in (or refreshing) on one device must never invalidate another device's session.
public class CrossDeviceConcurrencyTests
{
    [Fact]
    public async Task Logging_in_from_two_devices_keeps_both_sessions_independently_valid()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "multi-device@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Multi Device", "Student", "web-1"));

        var webLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", "web-1"));
        var mobileLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", "mobile-1"));

        var webTokens = await webLogin.Content.ReadFromJsonAsync<AuthResponse>();
        var mobileTokens = await mobileLogin.Content.ReadFromJsonAsync<AuthResponse>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var liveRows = await db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null).ToListAsync();
        Assert.Equal(2, liveRows.Count);

        var webRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(webTokens!.RefreshToken, "web-1"));
        Assert.Equal(HttpStatusCode.OK, webRefresh.StatusCode);

        var mobileRefreshAfterWebRefreshed = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(mobileTokens!.RefreshToken, "mobile-1"));
        Assert.Equal(HttpStatusCode.OK, mobileRefreshAfterWebRefreshed.StatusCode);
    }
}
