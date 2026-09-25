using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class ForgotPasswordEndpointTests
{
    [Fact]
    public async Task Forgot_password_for_a_registered_email_sends_a_reset_code_and_returns_204()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "forgot@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Forgot User", ["Student"], "device-1"));

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Registration itself already sent a confirmation email — assert on the reset one
        // specifically rather than assuming this is the only email sent to this address.
        var sent = Assert.Single(factory.EmailSender.SentEmails, e => e.To == email && e.Subject.Contains("Reset"));
        Assert.Matches("[A-Za-z0-9+/=_-]{20,}", sent.Body);
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_still_returns_204_but_sends_nothing()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password", new ForgotPasswordRequest("no-such-user@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentEmails);
    }

    public static string ExtractToken(string emailBody)
    {
        var match = Regex.Match(emailBody, "[A-Za-z0-9+/=_-]{20,}");
        Assert.True(match.Success, $"Could not find a reset token in email body: {emailBody}");
        return match.Value;
    }
}
