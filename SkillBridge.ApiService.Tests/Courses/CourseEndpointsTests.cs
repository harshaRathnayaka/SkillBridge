using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Courses.Contracts;
using SkillBridge.ApiService.Dashboard.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Courses;

public class CourseEndpointsTests
{
    private const string Password = "P@ssw0rd123!";

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email, string role, string displayName = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, displayName, role, $"device-{Guid.NewGuid():N}"));
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

    private static readonly CreateCourseRequest ValidCourse = new(
        "React Fundamentals", "Live", 50, "USD", " / hr", DateTimeOffset.UtcNow.AddDays(1));

    [Fact]
    public async Task Creating_a_course_without_auth_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", null, ValidCourse));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_student_cannot_create_a_course()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "not-a-tutor@example.com", "Student");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", auth.AccessToken, ValidCourse));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_creating_a_course_sees_it_on_their_own_dashboard()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "real-tutor@example.com", "Teacher", "Real Tutor");

        var before = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", auth.AccessToken));
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        var coursesBefore = beforeBody!.Tutor!.Courses.Count;

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", auth.AccessToken, ValidCourse));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var after = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", auth.AccessToken));
        var afterBody = await after.Content.ReadFromJsonAsync<DashboardResponse>();

        Assert.Equal(coursesBefore + 1, afterBody!.Tutor!.Courses.Count);
        Assert.Contains(afterBody.Tutor.Courses, c => c.Title == "React Fundamentals");
    }

    [Fact]
    public async Task A_student_enrolling_in_a_course_sees_it_on_their_own_dashboard_and_cannot_enroll_twice()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var tutorAuth = await RegisterAsync(client, "enroll-tutor@example.com", "Teacher", "Enroll Tutor");
        var studentAuth = await RegisterAsync(client, "enroll-student@example.com", "Student");

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", tutorAuth.AccessToken, ValidCourse));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponse>();

        var enrollResponse = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/courses/{created!.Id}/enroll", studentAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);

        var dashboard = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", studentAuth.AccessToken));
        var dashboardBody = await dashboard.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Contains(dashboardBody!.Student!.Courses, c => c.Title == "React Fundamentals");

        var duplicateEnroll = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/courses/{created.Id}/enroll", studentAuth.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, duplicateEnroll.StatusCode);
    }

    [Fact]
    public async Task Enrolling_in_a_nonexistent_course_returns_404()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var studentAuth = await RegisterAsync(client, "ghost-course-student@example.com", "Student");

        var response = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/courses/{Guid.NewGuid()}/enroll", studentAuth.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_and_publishing_a_material_updates_the_tutors_dashboard_counts()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var tutorAuth = await RegisterAsync(client, "material-tutor@example.com", "Teacher", "Material Tutor");

        var createCourse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", tutorAuth.AccessToken, ValidCourse));
        var course = await createCourse.Content.ReadFromJsonAsync<CreatedResponse>();

        var before = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", tutorAuth.AccessToken));
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        var draftsBefore = beforeBody!.Tutor!.DraftMaterials;
        var publishedBefore = beforeBody.Tutor.PublishedMaterials;

        var addMaterial = await client.SendAsync(BuildRequest(
            HttpMethod.Post, $"/api/courses/{course!.Id}/materials", tutorAuth.AccessToken, new CreateMaterialRequest("Lesson slides")));
        Assert.Equal(HttpStatusCode.OK, addMaterial.StatusCode);
        var material = await addMaterial.Content.ReadFromJsonAsync<CreatedResponse>();

        var afterAdd = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", tutorAuth.AccessToken));
        var afterAddBody = await afterAdd.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(draftsBefore + 1, afterAddBody!.Tutor!.DraftMaterials);

        var publish = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/courses/materials/{material!.Id}/publish", tutorAuth.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        var afterPublish = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", tutorAuth.AccessToken));
        var afterPublishBody = await afterPublish.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(draftsBefore, afterPublishBody!.Tutor!.DraftMaterials);
        Assert.Equal(publishedBefore + 1, afterPublishBody.Tutor.PublishedMaterials);
    }

    [Fact]
    public async Task A_tutor_cannot_add_material_to_another_tutors_course()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var ownerAuth = await RegisterAsync(client, "owner-tutor@example.com", "Teacher", "Owner Tutor");
        var otherAuth = await RegisterAsync(client, "other-tutor@example.com", "Teacher", "Other Tutor");

        var createCourse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/courses", ownerAuth.AccessToken, ValidCourse));
        var course = await createCourse.Content.ReadFromJsonAsync<CreatedResponse>();

        var response = await client.SendAsync(BuildRequest(
            HttpMethod.Post, $"/api/courses/{course!.Id}/materials", otherAuth.AccessToken, new CreateMaterialRequest("Not yours")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
