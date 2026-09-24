using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Dashboard.Contracts;
using SkillBridge.ApiService.Jobs.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Jobs;

public class JobEndpointsTests
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

    private static readonly CreateJobPostingRequest ValidPosting = new(
        "Senior Tutor", "Colombo, LK", "Hybrid", "Part-time", "LKR 3,000 / hr");

    [Fact]
    public async Task Posting_a_job_without_auth_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", null, ValidPosting));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_job_seeker_cannot_post_a_job()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "not-an-employer@example.com", "JobSeeker");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", auth.AccessToken, ValidPosting));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_employer_posting_a_job_sees_it_on_their_own_dashboard()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "real-employer@example.com", "JobGiver", "Real Employer");

        var before = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", auth.AccessToken));
        var beforeBody = await before.Content.ReadFromJsonAsync<DashboardResponse>();
        var openRolesBefore = beforeBody!.Employer!.OpenRoles;

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", auth.AccessToken, ValidPosting));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var after = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", auth.AccessToken));
        var afterBody = await after.Content.ReadFromJsonAsync<DashboardResponse>();

        Assert.Equal(openRolesBefore + 1, afterBody!.Employer!.OpenRoles);
    }

    [Fact]
    public async Task A_job_seeker_applying_sees_it_on_their_own_dashboard_and_cannot_apply_twice()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employerAuth = await RegisterAsync(client, "apply-employer@example.com", "JobGiver", "Apply Employer");
        var seekerAuth = await RegisterAsync(client, "apply-seeker@example.com", "JobSeeker");

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, ValidPosting));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponse>();

        var applyResponse = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/jobs/{created!.Id}/apply", seekerAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var dashboard = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", seekerAuth.AccessToken));
        var dashboardBody = await dashboard.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Contains(dashboardBody!.JobSeeker!.MyApplications, a => a.Title == "Senior Tutor");

        var duplicateApply = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/jobs/{created.Id}/apply", seekerAuth.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, duplicateApply.StatusCode);
    }

    [Fact]
    public async Task Applying_to_a_nonexistent_posting_returns_404()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var seekerAuth = await RegisterAsync(client, "ghost-job-seeker@example.com", "JobSeeker");

        var response = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/jobs/{Guid.NewGuid()}/apply", seekerAuth.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_employer_advancing_a_candidates_stage_moves_it_forward_once_per_call()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employerAuth = await RegisterAsync(client, "advance-employer@example.com", "JobGiver", "Advance Employer");
        var seekerAuth = await RegisterAsync(client, "advance-seeker@example.com", "JobSeeker");

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, ValidPosting));
        var posting = await createResponse.Content.ReadFromJsonAsync<CreatedResponse>();
        var applyResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting!.Id}/apply", seekerAuth.AccessToken));
        var application = await applyResponse.Content.ReadFromJsonAsync<CreatedResponse>();

        async Task<string> StageOf()
        {
            var dashboard = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", employerAuth.AccessToken));
            var body = await dashboard.Content.ReadFromJsonAsync<DashboardResponse>();
            return body!.Employer!.Candidates.Single(c => c.ApplicationId == application!.Id).Stage;
        }

        Assert.Equal("New", await StageOf());

        var advance1 = await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/applications/{application!.Id}/advance", employerAuth.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, advance1.StatusCode);
        Assert.Equal("Screening", await StageOf());

        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/applications/{application.Id}/advance", employerAuth.AccessToken));
        Assert.Equal("Interview", await StageOf());
    }

    [Fact]
    public async Task An_employer_cannot_advance_another_employers_candidate()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var ownerAuth = await RegisterAsync(client, "owner-employer@example.com", "JobGiver", "Owner Employer");
        var otherAuth = await RegisterAsync(client, "other-employer@example.com", "JobGiver", "Other Employer");
        var seekerAuth = await RegisterAsync(client, "isolation-seeker@example.com", "JobSeeker");

        var createResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", ownerAuth.AccessToken, ValidPosting));
        var posting = await createResponse.Content.ReadFromJsonAsync<CreatedResponse>();
        var applyResponse = await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting!.Id}/apply", seekerAuth.AccessToken));
        var application = await applyResponse.Content.ReadFromJsonAsync<CreatedResponse>();

        var response = await client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/jobs/applications/{application!.Id}/advance", otherAuth.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Browsing_the_job_catalog_without_auth_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/jobs", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_job_catalog_lists_every_posting_with_a_real_applicant_count_and_marks_the_callers_own_application()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employerAuth = await RegisterAsync(client, "catalog-employer@example.com", "JobGiver", "Catalog Employer");
        var seekerAuth = await RegisterAsync(client, "catalog-seeker@example.com", "JobSeeker");
        var otherSeekerAuth = await RegisterAsync(client, "catalog-other-seeker@example.com", "JobSeeker");

        var posting1 = await (await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, ValidPosting)))
            .Content.ReadFromJsonAsync<CreatedResponse>();
        var posting2Request = ValidPosting with { Title = "Another Role" };
        var posting2 = await (await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employerAuth.AccessToken, posting2Request)))
            .Content.ReadFromJsonAsync<CreatedResponse>();

        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting1!.Id}/apply", seekerAuth.AccessToken));
        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{posting1.Id}/apply", otherSeekerAuth.AccessToken));

        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/jobs", seekerAuth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalog = (await response.Content.ReadFromJsonAsync<List<JobCatalogItem>>())!;

        var item1 = catalog.Single(j => j.Id == posting1.Id);
        Assert.Equal(2, item1.ApplicantCount);
        Assert.True(item1.IsApplied);

        var item2 = catalog.Single(j => j.Id == posting2!.Id);
        Assert.Equal(0, item2.ApplicantCount);
        Assert.False(item2.IsApplied);
    }

    [Fact]
    public async Task An_employers_own_listings_only_shows_their_own_postings_with_real_applicant_counts()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employer1Auth = await RegisterAsync(client, "listings-employer-1@example.com", "JobGiver", "Listings Employer One");
        var employer2Auth = await RegisterAsync(client, "listings-employer-2@example.com", "JobGiver", "Listings Employer Two");
        var seekerAuth = await RegisterAsync(client, "listings-seeker@example.com", "JobSeeker");

        var ownPosting = await (await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employer1Auth.AccessToken, ValidPosting)))
            .Content.ReadFromJsonAsync<CreatedResponse>();
        await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employer2Auth.AccessToken, ValidPosting));
        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{ownPosting!.Id}/apply", seekerAuth.AccessToken));

        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/jobs/mine", employer1Auth.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = await response.Content.ReadFromJsonAsync<List<EmployerJobListingItem>>();

        var listing = Assert.Single(listings!);
        Assert.Equal(ownPosting.Id, listing.Id);
        Assert.Equal(1, listing.ApplicantCount);
    }

    [Fact]
    public async Task A_job_seeker_cannot_view_the_employer_only_listings_endpoint()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var seekerAuth = await RegisterAsync(client, "not-an-employer-listings@example.com", "JobSeeker");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/jobs/mine", seekerAuth.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
