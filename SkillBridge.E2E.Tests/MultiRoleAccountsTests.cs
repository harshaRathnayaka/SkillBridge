using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Covers the "one account, several roles" flow end to end: multi-select at registration, the
// active-role switcher, and adding a role later via /add-role. Exhaustive backend rule coverage
// (401/403/409, role-gating swaps) lives in SkillBridge.ApiService.Tests.Auth; these just prove
// the UI is wired up correctly.
public class MultiRoleAccountsTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Registering_with_two_roles_shows_a_switcher_that_changes_both_nav_and_dashboard()
    {
        await RegisterAsync(Page, "Two Role Person", UniqueEmail("e2e-two-role"), "P@ssw0rd123!", ["Teacher", "Student"]);

        // Registration order is preserved as the starting active role — Teacher was checked first.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Course studio" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Live classes")).ToBeVisibleAsync();

        var switcher = Page.Locator(".app-role-switcher");
        await Expect(switcher).ToBeVisibleAsync();

        await switcher.SelectOptionAsync("Student");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Find teachers" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Active courses", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Live classes")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Adding_a_role_later_makes_a_previously_blocked_page_reachable_without_logging_out()
    {
        await RegisterAsync(Page, "Add Role Person", UniqueEmail("e2e-add-role"), "P@ssw0rd123!", "Student");

        // Not a Teacher yet — the Teacher-gated page bounces back to Home.
        await Page.GotoAsync($"{BaseUrl}/course-studio");
        await Page.WaitForURLAsync($"{BaseUrl}/");
        await Expect(Page.GetByText("Active courses", new() { Exact = true })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/add-role");
        var tutorRow = Page.Locator(".dashboard-card", new() { HasText = "Tutor" });
        await Expect(tutorRow).ToBeVisibleAsync();
        await tutorRow.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();

        // AddRoleAsync redirects home, then the page is reachable directly, still signed in.
        await Page.WaitForURLAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/course-studio");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Course studio" })).ToBeVisibleAsync();
    }
}
