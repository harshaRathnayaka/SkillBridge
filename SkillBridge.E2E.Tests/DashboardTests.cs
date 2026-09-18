using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Drives the real dashboard end to end for each role — the ApiService-level assertions on
// exact seeded counts live in SkillBridge.ApiService.Tests.Dashboard.DashboardEndpointTests;
// these just prove the right dashboard actually renders on Home after a real login.
public class DashboardTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Student_sees_the_student_dashboard_after_registering()
    {
        await RegisterAsync(Page, "Dash Student", UniqueEmail("e2e-dash-student"), "P@ssw0rd123!", "Student");

        await Expect(Page.GetByText("Active courses")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Tutor_sees_the_tutor_dashboard_after_registering()
    {
        await RegisterAsync(Page, "Dash Tutor", UniqueEmail("e2e-dash-tutor"), "P@ssw0rd123!", "Teacher");

        await Expect(Page.GetByText("Live classes")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Employer_sees_the_employer_dashboard_after_registering()
    {
        await RegisterAsync(Page, "Dash Employer", UniqueEmail("e2e-dash-employer"), "P@ssw0rd123!", "JobGiver");

        await Expect(Page.GetByText("Open roles")).ToBeVisibleAsync();
    }

    [Test]
    public async Task JobSeeker_sees_the_jobseeker_dashboard_after_registering()
    {
        await RegisterAsync(Page, "Dash Seeker", UniqueEmail("e2e-dash-jobseeker"), "P@ssw0rd123!", "JobSeeker");

        await Expect(Page.GetByText("New matches")).ToBeVisibleAsync();
    }
}
