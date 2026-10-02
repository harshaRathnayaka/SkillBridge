using Microsoft.Playwright;

namespace SkillBridge.E2E.Tests;

// Drives the /profile page end to end: filling it in raises the job seeker dashboard's
// "Profile strength" tile, and the references card only appears for roles that can have one.
// Scoring rules themselves are covered in SkillBridge.ApiService.Tests.Profiles.
public class ProfileTests : SkillBridgeE2ETestBase
{
    [Test]
    public async Task JobSeeker_completing_their_profile_raises_strength_from_0_to_100_percent()
    {
        await RegisterAsync(Page, "Profile Seeker", UniqueEmail("e2e-profile-seeker"), "P@ssw0rd123!", "JobSeeker");

        var strengthTile = Page.Locator(".stat-tile", new() { HasText = "Profile strength" }).Locator(".stat-tile-value");
        await Expect(strengthTile).ToHaveTextAsync("0%");

        await Page.GotoAsync($"{BaseUrl}/profile");
        await Page.GetByPlaceholder("e.g. Maths tutor and curriculum designer").FillAsync("Maths tutor and curriculum designer");
        await Page.GetByPlaceholder("A few sentences about you").FillAsync("I have taught secondary maths for ten years.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save profile" }).ClickAsync();

        foreach (var skill in new[] { "Algebra", "Calculus", "Curriculum design" })
        {
            await Page.GetByPlaceholder("Add a skill").FillAsync(skill);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Add skill" }).ClickAsync();
            await Expect(Page.GetByText(skill, new() { Exact = true })).ToBeVisibleAsync();
        }

        await Page.GetByPlaceholder("Reference name").FillAsync("Dr. Jane Smith");
        await Page.GetByPlaceholder("Relationship").FillAsync("Former head of department");
        await Page.GetByPlaceholder("Email or phone (optional)").FillAsync("jane.smith@example.com");
        await Page.GetByPlaceholder("Comment (optional)").FillAsync("Outstanding colleague.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add reference" }).ClickAsync();
        await Expect(Page.GetByText("Dr. Jane Smith")).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(strengthTile).ToHaveTextAsync("100%");
    }

    [Test]
    public async Task Teacher_sees_the_endorsements_card_but_a_student_does_not()
    {
        await RegisterAsync(Page, "Profile Teacher", UniqueEmail("e2e-profile-teacher"), "P@ssw0rd123!", "Teacher");
        await Page.GotoAsync($"{BaseUrl}/profile");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add reference" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Endorsements", new() { Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/login");

        await RegisterAsync(Page, "Profile Student", UniqueEmail("e2e-profile-student"), "P@ssw0rd123!", "Student");
        await Page.GotoAsync($"{BaseUrl}/profile");
        // Wait for the page itself to load before asserting something is absent.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save profile" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add reference" })).ToHaveCountAsync(0);
    }
}
