using System.Net;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class AccountLockoutTests
{
    private const string Password = "P@ssw0rd123!";

    // Matches the MaxFailedAccessAttempts configured in Program.cs.
    private const int MaxFailedAttempts = 5;

    private static async Task RegisterAsync(HttpClient client, string email) =>
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, "Lockout User", ["Student"], "device-1"))).EnsureSuccessStatusCode();

    [Fact]
    public async Task The_attempt_that_crosses_the_failure_threshold_locks_the_account_immediately()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "lockout-trip@example.com";
        await RegisterAsync(client, email);

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-{i}"));
        }

        Assert.Equal(HttpStatusCode.Locked, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task A_locked_account_rejects_even_the_correct_password()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "lockout-correct-pw@example.com";
        await RegisterAsync(client, email);

        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-{i}"));
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password, "device-final"));

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
    }

    [Fact]
    public async Task A_successful_login_resets_the_failed_attempt_counter()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "lockout-reset@example.com";
        await RegisterAsync(client, email);

        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-{i}"));
        }

        var goodLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password, "device-good"));
        Assert.Equal(HttpStatusCode.OK, goodLogin.StatusCode);

        // Another near-threshold burst of failures should not lock the account, since the
        // successful login above should have reset the counter back to zero.
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-second-{i}"));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task Failed_attempts_below_the_threshold_do_not_lock_the_account()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "lockout-below-threshold@example.com";
        await RegisterAsync(client, email);

        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!", $"device-{i}"));
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password, "device-final"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
