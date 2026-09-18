using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Dashboard.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Dashboard;

public class DashboardEndpointTests
{
    private const string Password = "P@ssw0rd123!";

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email, string role, string displayName = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, displayName, role, $"device-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<HttpResponseMessage> GetDashboardAsync(HttpClient client, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Dashboard_without_a_bearer_token_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await GetDashboardAsync(client, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Student_dashboard_reflects_their_own_seeded_enrollments()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-student@example.com", "Student");

        var response = await GetDashboardAsync(client, auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(body);
        Assert.Equal("Student", body!.Role);
        Assert.NotNull(body.Student);
        Assert.Null(body.Tutor);
        Assert.Null(body.Employer);
        Assert.Null(body.JobSeeker);

        Assert.Equal(2, body.Student!.ActiveCourses);
        Assert.Equal(2, body.Student.Courses.Count);
        Assert.True(body.Student.LessonsRemainingTotal > 0);
        Assert.InRange(body.Student.AvgProgressPercent, 0, 100);
        Assert.NotEmpty(body.Student.SuggestedTeachers);
    }

    [Fact]
    public async Task Tutor_dashboard_reflects_their_own_seeded_courses_and_materials()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-tutor@example.com", "Teacher");

        var response = await GetDashboardAsync(client, auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(body?.Tutor);
        Assert.Null(body!.Student);

        Assert.Equal(2, body.Tutor!.Courses.Count);
        Assert.Equal(1, body.Tutor.LiveClasses);
        Assert.True(body.Tutor.LearnersEnrolledTotal > 0);
        Assert.Equal(1, body.Tutor.PublishedMaterials);
        Assert.Equal(1, body.Tutor.DraftMaterials);
        Assert.NotEqual("—", body.Tutor.EarningsLabel);
        Assert.NotNull(body.Tutor.RatingAverage);
    }

    [Fact]
    public async Task Employer_dashboard_reflects_their_own_seeded_postings_and_candidates()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-employer@example.com", "JobGiver");

        var response = await GetDashboardAsync(client, auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(body?.Employer);

        Assert.Equal(1, body!.Employer!.OpenRoles);
        Assert.Equal(2, body.Employer.Applicants);
        Assert.Equal(1, body.Employer.Interviews);
        Assert.Equal(1, body.Employer.NeedReview);
        Assert.NotEmpty(body.Employer.Candidates);
        Assert.NotEmpty(body.Employer.UpcomingSessions);
    }

    [Fact]
    public async Task JobSeeker_dashboard_reflects_their_own_application_and_shared_job_catalog()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-jobseeker@example.com", "JobSeeker");

        var response = await GetDashboardAsync(client, auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(body?.JobSeeker);

        Assert.Equal(1, body!.JobSeeker!.ApplicationsCount);
        Assert.Equal(1, body.JobSeeker.AtInterview);
        Assert.NotEmpty(body.JobSeeker.MyApplications);
        // The catalog has 3 system postings; one is already applied to, so up to 2 remain as
        // "new matches" (plus any real postings from other employers, none exist in this test).
        Assert.NotEmpty(body.JobSeeker.Matches);
        Assert.DoesNotContain(body.JobSeeker.Matches, m => m.Title == body.JobSeeker.MyApplications[0].Title);
    }

    [Fact]
    public async Task A_tutors_dashboard_never_includes_another_tutors_courses()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var authA = await RegisterAsync(client, "dash-tutor-a@example.com", "Teacher");
        await RegisterAsync(client, "dash-tutor-b@example.com", "Teacher");

        var response = await GetDashboardAsync(client, authA.AccessToken);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        // Each tutor gets their own 2 seeded courses — if isolation broke, tutor A would see 4.
        Assert.Equal(2, body!.Tutor!.Courses.Count);
    }
}
