using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// The full forgot->reset round trip (with a real token) is covered at the API level in
// SkillBridge.ApiService.Tests.Auth.ResetPasswordEndpointTests, using a recording email
// sender to capture the token — there's no real mailbox to read from a browser in E2E.
// These tests cover what's actually reachable through the UI: the request form and the
// guaranteed-fail path of an unknown/garbage reset code.
public class ForgotPasswordTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Requesting_a_reset_shows_the_same_confirmation_regardless_of_whether_the_email_is_registered()
    {
        await Page.GotoAsync($"{BaseUrl}/forgot-password");
        await Page.GetByPlaceholder("you@example.com").FillAsync(UniqueEmail("never-registered"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send reset code" }).ClickAsync();

        await Expect(Page.GetByText("Check your email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Resetting_with_an_unknown_code_shows_an_error_and_does_not_sign_the_user_in()
    {
        var email = UniqueEmail("reset-bad-code");
        await RegisterAsync(Page, "Bad Code User", email, "P@ssw0rd123!", "Student");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await Page.GotoAsync($"{BaseUrl}/reset-password");
        await Page.GetByPlaceholder("you@example.com").FillAsync(email);
        await Page.GetByPlaceholder("Paste the code from your email").FillAsync("not-a-real-code");
        await Page.GetByPlaceholder("At least 8 characters").FillAsync("N3wP@ssword456!");
        await Page.GetByPlaceholder("Re-enter your new password").FillAsync("N3wP@ssword456!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset password" }).ClickAsync();

        await Expect(Page.GetByText("Invalid or expired reset code.")).ToBeVisibleAsync();
    }
}
