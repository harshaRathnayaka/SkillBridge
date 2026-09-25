using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Courses.Contracts;
using SkillBridge.ApiService.Dashboard.Contracts;
using SkillBridge.ApiService.Jobs.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Dashboard;

// There is no seeded demo data (see DashboardContentSeeder's removal) — every number on every
// dashboard comes from real rows, so these tests build up exactly the state they assert on via
// the real Courses/Jobs endpoints rather than relying on any starter fixture.
public class DashboardEndpointTests
{
    private const string Password = "P@ssw0rd123!";

    private static readonly CreateCourseRequest ValidCourse = new(
        "React Fundamentals", "Live", 50, "USD", " / hr", DateTimeOffset.UtcNow.AddDays(1));

    private static readonly CreateJobPostingRequest ValidPosting = new(
        "Senior Tutor", "Colombo, LK", "Hybrid", "Part-time", "LKR 3,000 / hr");

    private static Task<AuthResponse> RegisterAsync(HttpClient client, string email, string role, string displayName = "Test User") =>
        RegisterAsync(client, email, [role], displayName);

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email, IReadOnlyList<string> roles, string displayName = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, displayName, roles, $"device-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string? accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> GetDashboardAsync(HttpClient client, string? accessToken, string? activeRole = null)
    {
        var url = activeRole is null ? "/api/dashboard" : $"/api/dashboard?role={activeRole}";
        return await client.SendAsync(BuildRequest(HttpMethod.Get, url, accessToken));
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
    public async Task Student_dashboard_starts_at_zero_and_only_reflects_their_own_real_enrollment()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var studentAuth = await RegisterAsync(client, "dash-student@example.com", "Student");

        var beforeAnyCourseExists = await GetDashboardAsync(client, studentAuth.AccessToken);
        var beforeBody = await beforeAnyCourseExists.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal("Student", beforeBody!.Role);
        Assert.Null(beforeBody.Tutor);
        Assert.Equal(0, beforeBody.Student!.ActiveCourses);
        Assert.Empty(beforeBody.Student.Courses);
        Assert.Empty(beforeBody.Student.SuggestedTeachers);

        var tutorAuth = await RegisterAsync(client, "dash-student-tutor@example.com", "Teacher", "Course Owner");
        var createCourse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", tutorAuth.AccessToken, ValidCourse));
        var course = await createCourse.Content.ReadFromJsonAsync<CreatedResponse>();

        var afterCourseExists = await GetDashboardAsync(client, studentAuth.AccessToken);
        var afterCourseBody = await afterCourseExists.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Single(afterCourseBody!.Student!.SuggestedTeachers);
        Assert.Equal(0, afterCourseBody.Student.ActiveCourses);

        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/courses/{course!.Id}/enroll", studentAuth.AccessToken));

        var afterEnrolling = await GetDashboardAsync(client, studentAuth.AccessToken);
        var afterEnrollingBody = await afterEnrolling.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(1, afterEnrollingBody!.Student!.ActiveCourses);
        Assert.Equal("React Fundamentals", afterEnrollingBody.Student.Courses[0].Title);
    }

    [Fact]
    public async Task Tutor_dashboard_starts_at_zero_and_reflects_their_own_real_courses_and_materials()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var tutorAuth = await RegisterAsync(client, "dash-tutor@example.com", "Teacher");

        var before = await GetDashboardAsync(client, tutorAuth.AccessToken);
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal("Teacher", beforeBody!.Role);
        Assert.Empty(beforeBody.Tutor!.Courses);
        Assert.Equal(0, beforeBody.Tutor.LiveClasses);
        Assert.Equal("—", beforeBody.Tutor.EarningsLabel);
        Assert.Null(beforeBody.Tutor.RatingAverage);

        var createCourse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", tutorAuth.AccessToken, ValidCourse));
        var course = await createCourse.Content.ReadFromJsonAsync<CreatedResponse>();
        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/courses/{course!.Id}/materials", tutorAuth.AccessToken, new CreateMaterialRequest("Lesson slides")));

        var after = await GetDashboardAsync(client, tutorAuth.AccessToken);
        var afterBody = await after.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Single(afterBody!.Tutor!.Courses);
        Assert.Equal(1, afterBody.Tutor.LiveClasses);
        Assert.Equal(1, afterBody.Tutor.DraftMaterials);
        Assert.Equal(0, afterBody.Tutor.PublishedMaterials);
    }

    [Fact]
    public async Task Employer_dashboard_starts_at_zero_and_reflects_their_own_real_postings_and_candidates()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employerAuth = await RegisterAsync(client, "dash-employer@example.com", "JobGiver");

        var before = await GetDashboardAsync(client, employerAuth.AccessToken);
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(0, beforeBody!.Employer!.OpenRoles);
        Assert.Empty(beforeBody.Employer.Candidates);

        var createPosting = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, ValidPosting));
        var posting = await createPosting.Content.ReadFromJsonAsync<CreatedResponse>();

        var seekerAuth = await RegisterAsync(client, "dash-employer-seeker@example.com", "JobSeeker", "Applicant One");
        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting!.Id}/apply", seekerAuth.AccessToken));

        var after = await GetDashboardAsync(client, employerAuth.AccessToken);
        var afterBody = await after.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(1, afterBody!.Employer!.OpenRoles);
        Assert.Equal(1, afterBody.Employer.Applicants);
        Assert.Equal("Applicant One", afterBody.Employer.Candidates[0].Name);
    }

    [Fact]
    public async Task JobSeeker_dashboard_starts_at_zero_and_only_shows_real_postings_as_matches()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var seekerAuth = await RegisterAsync(client, "dash-jobseeker@example.com", "JobSeeker");

        var before = await GetDashboardAsync(client, seekerAuth.AccessToken);
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(0, beforeBody!.JobSeeker!.ApplicationsCount);
        Assert.Empty(beforeBody.JobSeeker.Matches);

        var employerAuth = await RegisterAsync(client, "dash-jobseeker-employer@example.com", "JobGiver");
        var createPosting = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, ValidPosting));
        var posting = await createPosting.Content.ReadFromJsonAsync<CreatedResponse>();

        var afterPostingExists = await GetDashboardAsync(client, seekerAuth.AccessToken);
        var afterPostingBody = await afterPostingExists.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Single(afterPostingBody!.JobSeeker!.Matches);

        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting!.Id}/apply", seekerAuth.AccessToken));

        var afterApplying = await GetDashboardAsync(client, seekerAuth.AccessToken);
        var afterApplyingBody = await afterApplying.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(1, afterApplyingBody!.JobSeeker!.ApplicationsCount);
        Assert.Empty(afterApplyingBody.JobSeeker.Matches);
        Assert.Equal("Senior Tutor", afterApplyingBody.JobSeeker.MyApplications[0].Title);
    }

    [Fact]
    public async Task A_tutors_dashboard_never_includes_another_tutors_courses()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var authA = await RegisterAsync(client, "dash-tutor-a@example.com", "Teacher");
        var authB = await RegisterAsync(client, "dash-tutor-b@example.com", "Teacher");

        await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", authA.AccessToken, ValidCourse));
        await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", authB.AccessToken, ValidCourse with { Title = "Other Tutor's Course" }));

        var response = await GetDashboardAsync(client, authA.AccessToken);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        Assert.Single(body!.Tutor!.Courses);
        Assert.Equal("React Fundamentals", body.Tutor.Courses[0].Title);
    }

    [Fact]
    public async Task A_multi_role_accounts_active_role_query_param_picks_which_section_is_built_and_AllRoles_lists_everything_held()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-multi-role@example.com", ["Teacher", "Student"]);

        var asTeacher = await GetDashboardAsync(client, auth.AccessToken, "Teacher");
        var teacherBody = await asTeacher.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal("Teacher", teacherBody!.Role);
        Assert.NotNull(teacherBody.Tutor);
        Assert.Null(teacherBody.Student);
        Assert.Equal(["Student", "Teacher"], teacherBody.AllRoles.OrderBy(r => r));

        var asStudent = await GetDashboardAsync(client, auth.AccessToken, "Student");
        var studentBody = await asStudent.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal("Student", studentBody!.Role);
        Assert.NotNull(studentBody.Student);
        Assert.Null(studentBody.Tutor);
        Assert.Equal(["Student", "Teacher"], studentBody.AllRoles.OrderBy(r => r));
    }

    [Fact]
    public async Task Omitting_the_role_query_param_still_returns_a_valid_single_section()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-no-role-param@example.com", ["Teacher", "Student"]);

        var response = await GetDashboardAsync(client, auth.AccessToken);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body!.Role is "Teacher" or "Student");
        Assert.True(body.Tutor is not null || body.Student is not null);
        Assert.Equal(["Student", "Teacher"], body.AllRoles.OrderBy(r => r));
    }
}
