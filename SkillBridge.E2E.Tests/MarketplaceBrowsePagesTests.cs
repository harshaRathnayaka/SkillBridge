using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Drives each role's new browse/studio/pipeline page end to end — the pages introduced to
// match https://skill-build-hire.lovable.app's "Find teachers"/"Course studio"/"Post &
// pipeline"/"Find work" nav links, which previously rendered as inert "coming soon" labels.
// Exhaustive rule coverage (401s, 403s, ownership checks) lives in
// SkillBridge.ApiService.Tests.Courses/Jobs; these just prove the pages are wired up correctly.
public class MarketplaceBrowsePagesTests : SkillBridgeE2ETestBase
{
    // The dev SQLite database (unlike the xUnit suite's per-test in-memory DB) persists across
    // E2E runs, so a fixed literal title would collide with rows a previous run already left
    // behind — every title these tests create is suffixed uniquely to stay collision-proof.
    private static string UniqueTitle(string label) => $"{label} {Guid.NewGuid():N}";

    [Test]
    public async Task Student_browses_find_teachers_and_enrolls()
    {
        var courseTitle = UniqueTitle("Browse Target Course");
        await RegisterAsync(Page, "Browse Tutor", UniqueEmail("e2e-browse-tutor"), "P@ssw0rd123!", "Teacher");
        await Page.GetByRole(AriaRole.Button, new() { Name = "+ New course" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Algebra Fundamentals").FillAsync(courseTitle);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create course", Exact = true }).ClickAsync();
        await Expect(Page.GetByText(courseTitle)).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Browse Student", UniqueEmail("e2e-browse-student"), "P@ssw0rd123!", "Student");
        await Page.GotoAsync($"{BaseUrl}/find-teachers");

        var courseCard = Page.Locator(".dashboard-card", new() { HasText = courseTitle });
        await Expect(courseCard).ToBeVisibleAsync();
        await courseCard.GetByRole(AriaRole.Button, new() { Name = "Enroll", Exact = true }).ClickAsync();

        await Expect(courseCard.GetByText("Enrolled", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Tutor_unpublishes_a_material_from_course_studio()
    {
        await RegisterAsync(Page, "Studio Tutor", UniqueEmail("e2e-studio-tutor"), "P@ssw0rd123!", "Teacher");
        await Page.GotoAsync($"{BaseUrl}/course-studio");

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Create a class" }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Algebra Fundamentals").FillAsync("Studio Class");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create class", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Studio Class")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Course materials" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Publish new material" }).ClickAsync();
        await Page.GetByPlaceholder("Material title").FillAsync("Studio Material");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Publish new material", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Unpublish" })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Unpublish" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Publish", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Employers_pipeline_groups_a_candidate_by_stage_and_listings_shows_a_real_applicant_count()
    {
        var jobTitle = UniqueTitle("Pipeline Target Role");
        var employerEmail = UniqueEmail("e2e-pipeline-employer");
        await RegisterAsync(Page, "Pipeline Employer", employerEmail, "P@ssw0rd123!", "JobGiver");
        await Page.GotoAsync($"{BaseUrl}/post-pipeline");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post a job", Exact = true }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Senior Maths Tutor").FillAsync(jobTitle);
        await Page.GetByPlaceholder("e.g. Colombo, LK").FillAsync("Remote");
        await Page.GetByPlaceholder("Remote / Hybrid").FillAsync("Remote");
        await Page.GetByPlaceholder("Part-time").FillAsync("Contract");
        await Page.GetByPlaceholder("e.g. LKR 3,500 / hr").FillAsync("$40 / hr");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post job" }).ClickAsync();
        // Posting switches the page back to the Pipeline tab, which hides this form's own
        // submit button — the success signal, same idiom the dashboard's job-posting test uses.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Post job" })).Not.ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Pipeline Seeker", UniqueEmail("e2e-pipeline-seeker"), "P@ssw0rd123!", "JobSeeker");
        await Page.GotoAsync($"{BaseUrl}/find-work");
        await Expect(Page.GetByText(jobTitle)).ToBeVisibleAsync();
        await Page.Locator(".dashboard-card", new() { HasText = jobTitle })
            .GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".dashboard-card", new() { HasText = jobTitle }).GetByText("Applied")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await LoginAsync(Page, employerEmail, "P@ssw0rd123!");
        await Page.GotoAsync($"{BaseUrl}/post-pipeline");
        var newColumn = Page.Locator(".kanban-column", new() { HasText = "New" });
        await Expect(newColumn.GetByText("Pipeline Seeker")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "My listings", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".dashboard-card", new() { HasText = jobTitle }).GetByText("1 applicant")).ToBeVisibleAsync();
    }

    [Test]
    public async Task JobSeeker_browses_find_work_filters_by_work_mode_and_applies()
    {
        var jobTitle = UniqueTitle("Filter Target Role");
        await RegisterAsync(Page, "Filter Employer", UniqueEmail("e2e-filter-employer"), "P@ssw0rd123!", "JobGiver");
        await Page.GotoAsync($"{BaseUrl}/post-pipeline");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post a job", Exact = true }).ClickAsync();
        await Page.GetByPlaceholder("e.g. Senior Maths Tutor").FillAsync(jobTitle);
        await Page.GetByPlaceholder("e.g. Colombo, LK").FillAsync("Colombo, LK");
        await Page.GetByPlaceholder("Remote / Hybrid").FillAsync("On-site");
        await Page.GetByPlaceholder("Part-time").FillAsync("Full-time");
        await Page.GetByPlaceholder("e.g. LKR 3,500 / hr").FillAsync("LKR 3,000 / hr");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post job" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Filter Seeker", UniqueEmail("e2e-filter-seeker"), "P@ssw0rd123!", "JobSeeker");
        await Page.GotoAsync($"{BaseUrl}/find-work");
        await Expect(Page.GetByText(jobTitle)).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remote", Exact = true }).ClickAsync();
        await Expect(Page.GetByText(jobTitle)).Not.ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "On-site", Exact = true }).ClickAsync();
        await Expect(Page.GetByText(jobTitle)).ToBeVisibleAsync();
        await Page.Locator(".dashboard-card", new() { HasText = jobTitle })
            .GetByRole(AriaRole.Button, new() { Name = "Apply", Exact = true }).ClickAsync();

        await Expect(Page.Locator(".dashboard-card", new() { HasText = jobTitle }).GetByText("Applied")).ToBeVisibleAsync();
    }
}
