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

    // Playwright's default 5s Expect timeout assumes a local database. A page that loads after a
    // fresh login makes several sequential API calls, and against a remote database (e.g. Neon,
    // ~270ms+ per query) that can exceed 5s even though the data is correct.
    [OneTimeSetUp]
    public void ConfigureExpectTimeout() =>
        Assertions.SetDefaultExpectTimeout(
            int.TryParse(Environment.GetEnvironmentVariable("SKILLBRIDGE_E2E_EXPECT_TIMEOUT_MS"), out var ms) ? ms : 15_000);

    protected static string UniqueEmail(string label) =>
        $"{label}-{Guid.NewGuid():N}@example.com";

    protected async Task RegisterAsync(IPage page, string displayName, string email, string password, string role) =>
        await RegisterAsync(page, displayName, email, password, [role]);

    // Register.razor's role field is a checkbox per role (id="role-{RawRoleName}", e.g.
    // "role-Teacher") rather than a single-select, since one account can hold several roles.
    protected async Task RegisterAsync(IPage page, string displayName, string email, string password, IReadOnlyList<string> roles)
    {
        await page.GotoAsync($"{BaseUrl}/register");
        await page.GetByPlaceholder("Ada Lovelace").FillAsync(displayName);
        await page.GetByPlaceholder("you@example.com").FillAsync(email);
        await page.GetByPlaceholder("At least 8 characters").FillAsync(password);
        await page.GetByPlaceholder("Re-enter your password").FillAsync(password);
        foreach (var role in roles)
        {
            await page.Locator($"#role-{role}").CheckAsync();
        }
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
