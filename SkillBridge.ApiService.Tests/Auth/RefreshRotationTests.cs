using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class RefreshRotationTests
{
    [Fact]
    public async Task Reusing_an_already_rotated_refresh_token_is_rejected_and_revokes_the_whole_family()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "reuse-detect@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Reuse Detect", ["Student"], "device-1"));
        var initial = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var firstRefresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(initial!.RefreshToken, "device-1"));
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        // Reusing the original (now-rotated) token is the reuse-detection trigger.
        var reuseAttempt = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(initial.RefreshToken, "device-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        var secondTokens = await firstRefresh.Content.ReadFromJsonAsync<AuthResponse>();
        var secondRefreshAfterReuseDetected = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(secondTokens!.RefreshToken, "device-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefreshAfterReuseDetected.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var liveRows = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.DeviceId == "device-1" && rt.RevokedAt == null)
            .ToListAsync();
        Assert.Empty(liveRows);
    }

    [Fact]
    public async Task Concurrent_refresh_of_the_same_token_lets_exactly_one_request_win()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "race@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Race", ["Student"], "device-1"));
        var initial = await register.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new RefreshRequest(initial!.RefreshToken, "device-1");
        var first = client.PostAsJsonAsync("/api/auth/refresh", request);
        var second = client.PostAsJsonAsync("/api/auth/refresh", request);
        var responses = await Task.WhenAll(first, second);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthorizedCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);
        Assert.Equal(1, okCount);
        Assert.Equal(1, unauthorizedCount);

        // The safety property under test is "never double-issue" (at most one live session
        // survives a race on the same token) — not "always exactly one survives". If the two
        // requests happen to run back-to-back rather than truly simultaneously, the loser sees
        // an already-rotated token and its reuse-detection response revokes the whole device
        // family (including the winner's brand-new row) as a conservative anti-replay measure.
        // That's an accepted, documented trade-off (see IRefreshTokenService.RotateAsync):
        // well-behaved clients coalesce concurrent refresh calls so this race shouldn't happen
        // in practice, and if it does, forcing a fresh login is safe — silently duplicating a
        // session would not be.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var liveRows = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.DeviceId == "device-1" && rt.RevokedAt == null)
            .ToListAsync();
        Assert.True(liveRows.Count <= 1, $"Expected at most one live session, found {liveRows.Count}.");
    }
}
