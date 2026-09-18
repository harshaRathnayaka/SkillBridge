using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class LoginEndpointTests
{
    private static async Task RegisterAsync(HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, password, "Test User", "Student", deviceId));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_tokens()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "login-ok@example.com", "P@ssw0rd123!", "device-register");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "login-ok@example.com", "P@ssw0rd123!", "device-login"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_with_wrong_password_and_unknown_email_return_the_same_generic_401()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "login-wrongpw@example.com", "P@ssw0rd123!", "device-register");

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "login-wrongpw@example.com", "NotTheRightPassword1!", "device-a"));
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "no-such-user@example.com", "AnyPassword1!", "device-b"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        var unknownEmailBody = await unknownEmailResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(unknownEmailBody!.Errors, wrongPasswordBody!.Errors);
    }

    [Fact]
    public async Task Login_creates_exactly_one_refresh_token_row_for_the_user_and_device()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "login-row@example.com", "P@ssw0rd123!", "device-register");

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "login-row@example.com", "P@ssw0rd123!", "device-login-row"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "login-row@example.com");
        var rows = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.DeviceId == "device-login-row")
            .ToListAsync();

        Assert.Single(rows);
    }
}
