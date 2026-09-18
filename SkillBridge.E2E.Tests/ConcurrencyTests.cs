using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Black-box version of the requirement that drove IRefreshTokenService's design (see
// SkillBridge.ApiService.Tests.Auth.CrossDeviceConcurrencyTests for the API-level tests):
// a user must be able to be signed in on two devices/sessions at once, and signing in on one
// must not sign the other out. Each Playwright BrowserContext has its own isolated
// localStorage, standing in for two separate devices (e.g. mobile + web).
public class ConcurrencyTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Logging_in_from_two_independent_sessions_keeps_both_signed_in()
    {
        var email = UniqueEmail("concurrent");
        const string password = "P@ssw0rd123!";

        // "Device A": register, which also signs this session in.
        await RegisterAsync(Page, "Multi Device", email, password, "Teacher");
        await Expect(Page.GetByText("Welcome back, Multi Device")).ToBeVisibleAsync();

        // "Device B": a fully independent browser context/session, signing in as the same user.
        var contextB = await Browser.NewContextAsync();
        var pageB = await contextB.NewPageAsync();
        try
        {
            await LoginAsync(pageB, email, password);
            await Expect(pageB.GetByText("Welcome back, Multi Device")).ToBeVisibleAsync();

            // Device A must remain signed in — logging in on B must not have revoked A's session.
            await Page.ReloadAsync();
            await Expect(Page.GetByText("Welcome back, Multi Device")).ToBeVisibleAsync();
        }
        finally
        {
            await contextB.CloseAsync();
        }
    }

    [Test]
    public async Task Logging_out_of_one_session_does_not_sign_out_the_other()
    {
        var email = UniqueEmail("logout-concurrent");
        const string password = "P@ssw0rd123!";

        // "Device A": register, which also signs this session in.
        await RegisterAsync(Page, "Logout Device", email, password, "Student");
        await Expect(Page.GetByText("Welcome back, Logout Device")).ToBeVisibleAsync();

        // "Device B": a fully independent session signed in as the same user.
        var contextB = await Browser.NewContextAsync();
        var pageB = await contextB.NewPageAsync();
        try
        {
            await LoginAsync(pageB, email, password);
            await Expect(pageB.GetByText("Welcome back, Logout Device")).ToBeVisibleAsync();

            // Log out of Device A only.
            await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
            await Page.WaitForURLAsync($"{BaseUrl}/login");

            // Device A is signed out...
            await Page.GotoAsync($"{BaseUrl}/");
            await Expect(Page.GetByText("Welcome to SkillBridge")).ToBeVisibleAsync();

            // ...but Device B must still be signed in, unaffected by A's logout.
            await pageB.ReloadAsync();
            await Expect(pageB.GetByText("Welcome back, Logout Device")).ToBeVisibleAsync();
        }
        finally
        {
            await contextB.CloseAsync();
        }
    }
}
