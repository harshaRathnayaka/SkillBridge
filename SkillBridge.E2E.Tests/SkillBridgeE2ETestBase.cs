using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace SkillBridge.E2E.Tests;

// Drives the real, already-running app (SkillBridge.ApiService + SkillBridge.Web) end to end
// through a browser — these tests do not start the servers themselves. Run
// `dotnet run --project SkillBridge.AppHost` (or the Web + ApiService projects individually)
// first, then `dotnet test SkillBridge.E2E.Tests`, optionally overriding SKILLBRIDGE_E2E_BASE_URL.
public abstract class SkillBridgeE2ETestBase : PageTest
{
    protected static string BaseUrl =>
        Environment.GetEnvironmentVariable("SKILLBRIDGE_E2E_BASE_URL") ?? "http://localhost:5121";

    protected static string UniqueEmail(string label) =>
        $"{label}-{Guid.NewGuid():N}@example.com";

    protected async Task RegisterAsync(IPage page, string displayName, string email, string password, string role)
    {
        await page.GotoAsync($"{BaseUrl}/register");
        await page.GetByPlaceholder("Ada Lovelace").FillAsync(displayName);
        await page.GetByPlaceholder("you@example.com").FillAsync(email);
        await page.GetByPlaceholder("At least 8 characters").FillAsync(password);
        await page.GetByPlaceholder("Re-enter your password").FillAsync(password);
        await page.Locator("select").SelectOptionAsync(role);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await page.WaitForURLAsync($"{BaseUrl}/");
    }

    protected async Task LoginAsync(IPage page, string email, string password)
    {
        await page.GotoAsync($"{BaseUrl}/login");
        await page.GetByPlaceholder("you@example.com").FillAsync(email);
        await page.GetByPlaceholder("Enter your password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.WaitForURLAsync($"{BaseUrl}/");
    }
}
