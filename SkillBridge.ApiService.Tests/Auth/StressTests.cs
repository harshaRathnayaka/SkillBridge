using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

// Load/volume tests, as distinct from the correctness-under-a-single-race tests in
// RefreshRotationTests — these fire many requests at once and check the system holds up
// (no crashes, no deadlocks, no data corruption), not just that one specific race resolves
// correctly. Tagged "Stress" so a CI pipeline can run them separately from the fast unit
// suite if their cost ever becomes a problem.
[Trait("Category", "Stress")]
public class StressTests
{
    [Fact]
    public async Task Many_concurrent_registrations_of_distinct_users_all_succeed_with_no_duplicates()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const int userCount = 100;

        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(Enumerable.Range(0, userCount).Select(i => client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"stress-user-{i}@example.com", "P@ssw0rd123!", $"Stress User {i}", "Student", $"device-{i}"))));
        stopwatch.Stop();

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"100 concurrent registrations took {stopwatch.Elapsed}, expected well under 30s.");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(userCount, await db.Users.CountAsync());
        Assert.Equal(userCount, await db.RefreshTokens.CountAsync(rt => rt.RevokedAt == null));
    }

    [Fact]
    public async Task Many_concurrent_logins_from_distinct_devices_for_the_same_user_each_get_an_independent_working_session()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "stress-multidevice@example.com";
        const int deviceCount = 30;
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Stress Multi Device", "Student", "device-seed"));

        var loginResponses = await Task.WhenAll(Enumerable.Range(0, deviceCount).Select(i => client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", $"device-{i}"))));

        Assert.All(loginResponses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var payloads = await Task.WhenAll(loginResponses.Select(r => r.Content.ReadFromJsonAsync<AuthResponse>()));

        // Every device's freshly-issued refresh token must still work independently.
        var refreshResponses = await Task.WhenAll(payloads.Select((p, i) => client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(p!.RefreshToken, $"device-{i}"))));
        Assert.All(refreshResponses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var liveDeviceCount = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null && rt.DeviceId != "device-seed")
            .Select(rt => rt.DeviceId)
            .Distinct()
            .CountAsync();
        Assert.Equal(deviceCount, liveDeviceCount);
    }

    [Fact]
    public async Task A_burst_of_mixed_valid_and_invalid_login_attempts_never_crashes_the_endpoint()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "stress-mixed-login@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Stress Mixed", "Student", "device-seed"));

        var attempts = Enumerable.Range(0, 100).Select(i => i % 3 == 0
            ? client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", $"device-{i}"))
            : client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-{i}")));

        var responses = await Task.WhenAll(attempts);

        // With account lockout now in place (see AccountLockoutTests), this many concurrent
        // wrong-password attempts against one account will legitimately trip it — 423 is a
        // clean, expected rejection here, same as 200/401. Only a 5xx would mean the burst
        // actually broke something.
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Locked,
            $"Unexpected status {r.StatusCode} — the endpoint should only ever accept or reject cleanly, never error."));
    }
}
