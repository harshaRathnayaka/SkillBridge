using System.Net;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class ResetPasswordEndpointTests
{
    private const string OriginalPassword = "P@ssw0rd123!";
    private const string NewPassword = "N3wP@ssword456!";

    private static async Task<string> RegisterAndRequestResetAsync(HttpClient client, ApiServiceTestFactory factory, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, OriginalPassword, "Reset User", ["Student"], "device-1"));
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var sent = factory.EmailSender.SentEmails.Single(e => e.To == email && e.Subject.Contains("Reset"));
        return ForgotPasswordEndpointTests.ExtractToken(sent.Body);
    }

    [Fact]
    public async Task Reset_password_with_a_valid_token_changes_the_password()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "reset-ok@example.com";
        var token = await RegisterAndRequestResetAsync(client, factory, email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new ResetPasswordRequest(email, token, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, OriginalPassword, "device-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, NewPassword, "device-1"));
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_an_invalid_token_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "reset-bad-token@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, OriginalPassword, "Reset User", ["Student"], "device-1"));

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new ResetPasswordRequest(email, "not-a-real-token", NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_for_an_unknown_email_is_rejected_without_revealing_that()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest("no-such-user@example.com", "irrelevant-token", NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(["Invalid or expired reset code."], body!.Errors);
    }

    [Fact]
    public async Task A_reset_token_cannot_be_reused_after_a_successful_reset()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "reset-reuse@example.com";
        var token = await RegisterAndRequestResetAsync(client, factory, email);

        var firstReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new ResetPasswordRequest(email, token, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, firstReset.StatusCode);

        var secondReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new ResetPasswordRequest(email, token, "AnotherP@ss789!"));
        Assert.Equal(HttpStatusCode.BadRequest, secondReset.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_a_weak_new_password_is_rejected_with_a_helpful_message()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "reset-weak@example.com";
        var token = await RegisterAndRequestResetAsync(client, factory, email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new ResetPasswordRequest(email, token, "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotEmpty(body!.Errors);
        Assert.NotEqual(["Invalid or expired reset code."], body.Errors);
    }
}
