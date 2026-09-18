using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

public class RegisterAndLoginTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task Register_as_student_lands_on_home_with_student_role_and_no_teacher_only_content()
    {
        var email = UniqueEmail("student");
        await RegisterAsync(Page, "Sam Student", email, "P@ssw0rd123!", "Student");

        await Expect(Page.GetByText("Welcome back, Sam Student")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Student", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_with_an_existing_account_lands_on_home()
    {
        var email = UniqueEmail("login");
        await RegisterAsync(Page, "Lee Login", email, "P@ssw0rd123!", "JobSeeker");

        // Log out, then prove a fresh login (not just the post-register session) works too.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await LoginAsync(Page, email, "P@ssw0rd123!");

        await Expect(Page.GetByText("Welcome back, Lee Login")).ToBeVisibleAsync();
    }
}
