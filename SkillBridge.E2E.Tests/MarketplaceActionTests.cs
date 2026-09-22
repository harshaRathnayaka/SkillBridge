using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Drives each role's primary "make the dashboard real" action end to end through the actual
// UI. Exhaustive rule coverage (403s, 409s, ownership checks) lives in
// SkillBridge.ApiService.Tests.Courses/Jobs; these just prove the buttons are wired up.
public class MarketplaceActionTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Tutor_creating_a_course_sees_it_appear_on_their_own_dashboard()
    {
        await RegisterAsync(Page, "Action Tutor", UniqueEmail("e2e-action-tutor"), "P@ssw0rd123!", "Teacher");

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New course" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Algebra Fundamentals").FillAsync("Playwright-Created Course");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create course", Exact = true }).ClickAsync();

        await Expect(Page.GetByText("Playwright-Created Course")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Student_enrolling_in_a_suggested_course_increases_their_active_course_count()
    {
        // No seeded catalog exists anymore — a tutor has to create a real course first, or
        // there would be nothing for the student to see in "Suggested teachers" at all.
        await RegisterAsync(Page, "Enroll Setup Tutor", UniqueEmail("e2e-enroll-tutor"), "P@ssw0rd123!", "Teacher");
        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New course" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Algebra Fundamentals").FillAsync("Enroll Target Course");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create course", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Enroll Target Course")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Action Student", UniqueEmail("e2e-action-student"), "P@ssw0rd123!", "Student");

        var activeCoursesBefore = await Page.Locator(".stat-tile", new() { HasText = "Active courses" })
            .Locator(".stat-tile-value").InnerTextAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enroll", Exact = true }).First.ClickAsync();

        var activeCoursesLocator = Page.Locator(".stat-tile", new() { HasText = "Active courses" }).Locator(".stat-tile-value");
        await Expect(activeCoursesLocator).ToHaveTextAsync((int.Parse(activeCoursesBefore) + 1).ToString());
    }

    [Test]
    public async Task Employer_posting_a_job_sees_open_roles_increase()
    {
        await RegisterAsync(Page, "Action Employer", UniqueEmail("e2e-action-employer"), "P@ssw0rd123!", "JobGiver");

        var openRolesBefore = await Page.Locator(".stat-tile", new() { HasText = "Open roles" })
            .Locator(".stat-tile-value").InnerTextAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Post a job" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Senior Maths Tutor").FillAsync("Playwright QA Lead");
        await Page.GetByPlaceholder("e.g. Colombo, LK").FillAsync("Remote");
        await Page.GetByPlaceholder("Remote / Hybrid").FillAsync("Remote");
        await Page.GetByPlaceholder("Part-time").FillAsync("Full-time");
        await Page.GetByPlaceholder("e.g. LKR 3,500 / hr").FillAsync("$50 / hr");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post job" }).ClickAsync();

        // ClickAsync returns as soon as the event dispatches, not once the resulting
        // create-then-reload async chain finishes — Expect(...) auto-retries until the stat
        // tile actually reflects the new posting instead of racing a one-shot read.
        var openRolesLocator = Page.Locator(".stat-tile", new() { HasText = "Open roles" }).Locator(".stat-tile-value");
        await Expect(openRolesLocator).ToHaveTextAsync((int.Parse(openRolesBefore) + 1).ToString());
    }

    [Test]
    public async Task JobSeeker_applying_to_a_role_sees_it_appear_in_their_own_applications()
    {
        // No seeded catalog exists anymore — an employer has to post a real role first, or
        // "Newest roles for you" would be empty.
        await RegisterAsync(Page, "Apply Setup Employer", UniqueEmail("e2e-apply-employer"), "P@ssw0rd123!", "JobGiver");
        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Post a job" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Senior Maths Tutor").FillAsync("Apply Target Role");
        await Page.GetByPlaceholder("e.g. Colombo, LK").FillAsync("Remote");
        await Page.GetByPlaceholder("Remote / Hybrid").FillAsync("Remote");
        await Page.GetByPlaceholder("Part-time").FillAsync("Contract");
        await Page.GetByPlaceholder("e.g. LKR 3,500 / hr").FillAsync("$40 / hr");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post job" }).ClickAsync();
        // The employer's own dashboard only lists candidates, not their own postings, so the
        // success signal is the form closing (it only does that once the create actually
        // succeeds — see EmployerDashboard.razor's CreatePostingAsync) rather than the title
        // appearing anywhere on this page.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "+ Post a job" })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Action Seeker", UniqueEmail("e2e-action-seeker"), "P@ssw0rd123!", "JobSeeker");

        var matchTitle = await Page.Locator(".dashboard-card-title").First.InnerTextAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).First.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My applications" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".dashboard-side").GetByText(matchTitle)).ToBeVisibleAsync();
    }
}
