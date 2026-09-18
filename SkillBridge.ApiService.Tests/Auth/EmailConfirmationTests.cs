using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class EmailConfirmationTests
{
    private static async Task<string> RegisterAndExtractConfirmationTokenAsync(
        HttpClient client, ApiServiceTestFactory factory, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Confirm User", "Student", "device-1"));
        response.EnsureSuccessStatusCode();

        var sent = factory.EmailSender.SentEmails.Single(e => e.To == email && e.Subject.Contains("Confirm"));
        return ForgotPasswordEndpointTests.ExtractToken(sent.Body);
    }

    [Fact]
    public async Task Registering_sends_a_confirmation_email_and_the_account_starts_unconfirmed()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "confirm-sent@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Confirm User", "Student", "device-1"));

        Assert.Contains(factory.EmailSender.SentEmails, e => e.To == email && e.Subject.Contains("Confirm"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Confirming_with_a_valid_token_marks_the_account_confirmed()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "confirm-ok@example.com";
        var token = await RegisterAndExtractConfirmationTokenAsync(client, factory, email);

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(email, token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task Confirming_with_an_invalid_token_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "confirm-badtoken@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Confirm User", "Student", "device-1"));

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email", new ConfirmEmailRequest(email, "not-a-real-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirming_for_an_unknown_email_is_rejected_without_revealing_that()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email", new ConfirmEmailRequest("no-such-user@example.com", "irrelevant-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirming_again_with_the_same_token_is_a_harmless_no_op()
    {
        // Unlike password reset, confirming an already-confirmed email has no state to
        // double-spend (it's the same EmailConfirmed = true either way), so — matching
        // ASP.NET Core Identity's own ConfirmEmailAsync behavior — the token isn't
        // invalidated by a successful confirmation the way a reset token is by a reset.
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "confirm-reuse@example.com";
        var token = await RegisterAndExtractConfirmationTokenAsync(client, factory, email);
        await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(email, token));

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(email, token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Login_still_succeeds_for_an_unconfirmed_account()
    {
        // Confirmation is tracked (for future trust-badge/gating features) but does not yet
        // block login — see the session's conversation for why this scope was chosen.
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "confirm-not-required@example.com";
        const string password = "P@ssw0rd123!";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, password, "Confirm User", "Student", "device-1"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password, "device-2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
