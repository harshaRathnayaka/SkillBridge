using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Dashboard.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Jobs.Contracts;
using SkillBridge.ApiService.Profiles.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Profiles;

public class ProfileEndpointsTests
{
    private const string Password = "P@ssw0rd123!";

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

    private static async Task<ProfileResponse> GetProfileAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/profile", token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProfileResponse>())!;
    }

    private static Task<HttpResponseMessage> AddSkillAsync(HttpClient client, string token, string name) =>
        client.SendAsync(BuildRequest(HttpMethod.Post, "/api/profile/skills", token, new AddSkillRequest(name)));

    private static Task<HttpResponseMessage> AddReferenceAsync(HttpClient client, string token, string name = "Sam Jones", string relationship = "Former manager") =>
        client.SendAsync(BuildRequest(HttpMethod.Post, "/api/profile/references", token,
            new AddReferenceRequest(name, relationship, "sam@example.com", "Great to work with")));

    private static Task<HttpResponseMessage> SaveProfileAsync(HttpClient client, string token, string headline, string bio) =>
        client.SendAsync(BuildRequest(HttpMethod.Put, "/api/profile", token, new UpdateProfileRequest(headline, bio)));

    [Theory]
    [InlineData("GET", "/api/profile")]
    [InlineData("PUT", "/api/profile")]
    [InlineData("POST", "/api/profile/skills")]
    [InlineData("DELETE", "/api/profile/skills/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/profile/references")]
    [InlineData("DELETE", "/api/profile/references/00000000-0000-0000-0000-000000000001")]
    public async Task Every_profile_endpoint_rejects_unauthenticated_callers(string method, string url)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(BuildRequest(new HttpMethod(method), url, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_new_user_gets_an_empty_profile_with_zero_strength()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "fresh@example.com", "Student");

        var profile = await GetProfileAsync(client, auth.AccessToken);

        Assert.Equal("", profile.Headline);
        Assert.Equal("", profile.Bio);
        Assert.Empty(profile.Skills);
        Assert.Empty(profile.References);
        Assert.Equal(0, profile.StrengthPercent);
        Assert.Equal("Add a headline", profile.StrengthNote);
    }

    [Fact]
    public async Task Saving_the_profile_persists_a_trimmed_headline_and_bio()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "writer@example.com", "Teacher");

        var save = await SaveProfileAsync(client, auth.AccessToken, "  Maths tutor  ", "  Ten years teaching.  ");

        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        var profile = await GetProfileAsync(client, auth.AccessToken);
        Assert.Equal("Maths tutor", profile.Headline);
        Assert.Equal("Ten years teaching.", profile.Bio);
    }

    [Fact]
    public async Task Saving_the_profile_again_overwrites_the_previous_values()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "rewriter@example.com", "Student");
        await SaveProfileAsync(client, auth.AccessToken, "First", "First bio");

        await SaveProfileAsync(client, auth.AccessToken, "Second", "Second bio");

        var profile = await GetProfileAsync(client, auth.AccessToken);
        Assert.Equal("Second", profile.Headline);
        Assert.Equal("Second bio", profile.Bio);
    }

    [Fact]
    public async Task Saving_the_profile_does_not_touch_the_existing_dashboard_stat_columns()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "stats@example.com", "Teacher");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = (await db.Users.SingleAsync(u => u.Email == "stats@example.com")).Id;
            db.UserProfiles.Add(new UserProfile { UserId = userId, RatingAverage = 4.5m, SavedRoles = 3 });
            await db.SaveChangesAsync();
        }

        await SaveProfileAsync(client, auth.AccessToken, "Headline", "Bio");

        using var verify = factory.Services.CreateScope();
        var row = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserProfiles.SingleAsync();
        Assert.Equal("Headline", row.Headline);
        Assert.Equal(4.5m, row.RatingAverage);
        Assert.Equal(3, row.SavedRoles);
    }

    [Theory]
    [InlineData(121, 10)]
    [InlineData(10, 1001)]
    public async Task Saving_an_overlong_headline_or_bio_is_a_bad_request(int headlineLength, int bioLength)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"long-{headlineLength}-{bioLength}@example.com", "Student");

        var response = await SaveProfileAsync(client, auth.AccessToken, new string('h', headlineLength), new string('b', bioLength));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Saving_the_maximum_lengths_is_accepted()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "max@example.com", "Student");

        var response = await SaveProfileAsync(client, auth.AccessToken, new string('h', 120), new string('b', 1000));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Saving_a_profile_with_missing_text_treats_it_as_empty()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "nulls@example.com", "Student");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Put, "/api/profile", auth.AccessToken, new { }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", (await GetProfileAsync(client, auth.AccessToken)).Headline);
    }

    [Fact]
    public async Task Adding_a_skill_trims_and_collapses_whitespace_and_lists_it_on_the_profile()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "skilled@example.com", "JobSeeker");

        var add = await AddSkillAsync(client, auth.AccessToken, "  React   Native ");

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var created = await add.Content.ReadFromJsonAsync<CreatedResponse>();
        var profile = await GetProfileAsync(client, auth.AccessToken);
        var skill = Assert.Single(profile.Skills);
        Assert.Equal("React Native", skill.Name);
        Assert.Equal(created!.Id, skill.Id);
    }

    [Fact]
    public async Task Adding_the_same_skill_in_a_different_case_is_a_conflict()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dupe@example.com", "JobSeeker");
        await AddSkillAsync(client, auth.AccessToken, "React");

        var again = await AddSkillAsync(client, auth.AccessToken, "  rEACT ");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Single((await GetProfileAsync(client, auth.AccessToken)).Skills);
    }

    [Fact]
    public async Task Two_users_adding_the_same_skill_share_a_single_skill_row()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var tutor = await RegisterAsync(client, "tutor-skill@example.com", "Teacher");
        var seeker = await RegisterAsync(client, "seeker-skill@example.com", "JobSeeker");

        await AddSkillAsync(client, tutor.AccessToken, "Python");
        await AddSkillAsync(client, seeker.AccessToken, "PYTHON");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Skills.CountAsync());
        Assert.Equal(2, await db.UserSkills.CountAsync());
        Assert.Equal("Python", Assert.Single((await GetProfileAsync(client, seeker.AccessToken)).Skills).Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0123456789012345678901234567890123456789X")]
    public async Task Adding_a_blank_or_overlong_skill_is_a_bad_request(string name)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"badskill-{name.Length}@example.com", "Student");

        var response = await AddSkillAsync(client, auth.AccessToken, name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_skill_name_of_exactly_forty_characters_is_accepted()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "forty@example.com", "Student");

        var response = await AddSkillAsync(client, auth.AccessToken, new string('s', 40));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_user_cannot_hold_more_than_twenty_skills()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "many@example.com", "Student");
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await AddSkillAsync(client, auth.AccessToken, $"Skill {i}")).StatusCode);
        }

        var overLimit = await AddSkillAsync(client, auth.AccessToken, "One too many");

        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
    public async Task Removing_a_skill_drops_it_from_the_profile_but_keeps_the_shared_skill()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "remover@example.com", "Student");
        var skillId = (await (await AddSkillAsync(client, auth.AccessToken, "Chess")).Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var remove = await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/profile/skills/{skillId}", auth.AccessToken));

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Empty((await GetProfileAsync(client, auth.AccessToken)).Skills);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Skills.CountAsync());
    }

    [Fact]
    public async Task Removing_a_skill_you_do_not_have_is_not_found_and_does_not_affect_its_owner()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var owner = await RegisterAsync(client, "owner-skill@example.com", "Student");
        var other = await RegisterAsync(client, "other-skill@example.com", "Student");
        var skillId = (await (await AddSkillAsync(client, owner.AccessToken, "Guitar")).Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var remove = await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/profile/skills/{skillId}", other.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Single((await GetProfileAsync(client, owner.AccessToken)).Skills);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("JobGiver")]
    public async Task Roles_other_than_teacher_and_job_seeker_cannot_add_references(string role)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"noref-{role}@example.com", role);

        var response = await AddReferenceAsync(client, auth.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("JobSeeker")]
    public async Task Teachers_and_job_seekers_can_add_and_see_references(string role)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"ref-{role}@example.com", role);

        var add = await AddReferenceAsync(client, auth.AccessToken, "  Sam Jones ", " Former manager ");

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var reference = Assert.Single((await GetProfileAsync(client, auth.AccessToken)).References);
        Assert.Equal("Sam Jones", reference.Name);
        Assert.Equal("Former manager", reference.Relationship);
        Assert.Equal("sam@example.com", reference.Contact);
        Assert.Equal("Great to work with", reference.Comment);
    }

    [Fact]
    public async Task A_multi_role_account_with_a_job_seeker_role_can_add_references()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "multi-ref@example.com", ["Student", "JobSeeker"]);

        var add = await AddReferenceAsync(client, auth.AccessToken);

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
    }

    [Fact]
    public async Task Optional_reference_fields_may_be_omitted_and_blank_values_are_stored_as_absent()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "optional@example.com", "JobSeeker");

        var add = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/profile/references", auth.AccessToken,
            new AddReferenceRequest("Pat Lee", "Colleague", "   ", null)));

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var reference = Assert.Single((await GetProfileAsync(client, auth.AccessToken)).References);
        Assert.Null(reference.Contact);
        Assert.Null(reference.Comment);
    }

    [Theory]
    [InlineData("", "Manager")]
    [InlineData("   ", "Manager")]
    [InlineData("Sam", "")]
    [InlineData("Sam", "   ")]
    public async Task A_reference_needs_a_name_and_a_relationship(string name, string relationship)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"badref-{name.Length}-{relationship.Length}@example.com", "JobSeeker");

        var response = await AddReferenceAsync(client, auth.AccessToken, name, relationship);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(101, 10, 10, 10)]
    [InlineData(10, 101, 10, 10)]
    [InlineData(10, 10, 201, 10)]
    [InlineData(10, 10, 10, 501)]
    public async Task Overlong_reference_fields_are_a_bad_request(int name, int relationship, int contact, int comment)
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, $"longref-{name}-{relationship}-{contact}-{comment}@example.com", "JobSeeker");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/profile/references", auth.AccessToken,
            new AddReferenceRequest(new string('n', name), new string('r', relationship), new string('c', contact), new string('m', comment))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reference_fields_at_their_maximum_lengths_are_accepted()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "maxref@example.com", "JobSeeker");

        var response = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/profile/references", auth.AccessToken,
            new AddReferenceRequest(new string('n', 100), new string('r', 100), new string('c', 200), new string('m', 500))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_user_cannot_hold_more_than_ten_references()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "manyrefs@example.com", "JobSeeker");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await AddReferenceAsync(client, auth.AccessToken, $"Person {i}")).StatusCode);
        }

        var overLimit = await AddReferenceAsync(client, auth.AccessToken, "One too many");

        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
    public async Task Removing_your_own_reference_deletes_it()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "delref@example.com", "Teacher");
        var referenceId = (await (await AddReferenceAsync(client, auth.AccessToken)).Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var remove = await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/profile/references/{referenceId}", auth.AccessToken));

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Empty((await GetProfileAsync(client, auth.AccessToken)).References);
    }

    [Fact]
    public async Task Removing_someone_elses_reference_is_not_found_and_leaves_it_intact()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var owner = await RegisterAsync(client, "ref-owner@example.com", "JobSeeker");
        var other = await RegisterAsync(client, "ref-other@example.com", "JobSeeker");
        var referenceId = (await (await AddReferenceAsync(client, owner.AccessToken)).Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var remove = await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/profile/references/{referenceId}", other.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Single((await GetProfileAsync(client, owner.AccessToken)).References);
    }

    [Fact]
    public async Task A_completed_job_seeker_profile_scores_one_hundred_including_a_reference()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "complete-seeker@example.com", "JobSeeker");
        await SaveProfileAsync(client, auth.AccessToken, "Backend developer", "Five years of APIs.");
        foreach (var skill in new[] { "C#", "SQL", "Azure" })
        {
            await AddSkillAsync(client, auth.AccessToken, skill);
        }
        await AddReferenceAsync(client, auth.AccessToken);

        var profile = await GetProfileAsync(client, auth.AccessToken);

        Assert.Equal(100, profile.StrengthPercent);
        Assert.Null(profile.StrengthNote);
    }

    [Fact]
    public async Task A_student_can_reach_one_hundred_without_references()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "complete-student@example.com", "Student");
        await SaveProfileAsync(client, auth.AccessToken, "Learner", "Keen to learn.");
        foreach (var skill in new[] { "Maths", "Physics", "Chemistry" })
        {
            await AddSkillAsync(client, auth.AccessToken, skill);
        }

        Assert.Equal(100, (await GetProfileAsync(client, auth.AccessToken)).StrengthPercent);
    }

    [Fact]
    public async Task The_job_seeker_dashboard_profile_strength_reflects_the_computed_score()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "dash-seeker@example.com", "JobSeeker");

        var before = await GetJobSeekerDashboardAsync(client, auth.AccessToken);
        await SaveProfileAsync(client, auth.AccessToken, "Designer", "Pixels.");
        var after = await GetJobSeekerDashboardAsync(client, auth.AccessToken);

        Assert.Equal(0, before.ProfileStrengthPercent);
        Assert.Equal("Add a headline", before.ProfileStrengthNote);
        Assert.Equal(50, after.ProfileStrengthPercent);
        Assert.Equal("Add 3 more skills", after.ProfileStrengthNote);
    }

    [Fact]
    public async Task An_employer_sees_the_applicants_profile_headline_on_their_candidate()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employer = await RegisterAsync(client, "hiring@example.com", "JobGiver");
        var seeker = await RegisterAsync(client, "applicant@example.com", "JobSeeker", "Alex Applicant");
        await SaveProfileAsync(client, seeker.AccessToken, "Senior data engineer", "Pipelines.");
        var posting = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employer.AccessToken,
            new CreateJobPostingRequest("Data Engineer", "Remote", "Remote", "Contract", "$50 / hr")));
        var jobId = (await posting.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{jobId}/apply", seeker.AccessToken));

        var dashboard = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", employer.AccessToken));
        var body = await dashboard.Content.ReadFromJsonAsync<DashboardResponse>();

        var candidate = Assert.Single(body!.Employer!.Candidates);
        Assert.Equal("Senior data engineer", candidate.Headline);
    }

    [Fact]
    public async Task An_applicant_without_a_profile_still_applies_with_an_empty_headline()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var employer = await RegisterAsync(client, "hiring2@example.com", "JobGiver");
        var seeker = await RegisterAsync(client, "plain-applicant@example.com", "JobSeeker");
        var posting = await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/jobs", employer.AccessToken,
            new CreateJobPostingRequest("Analyst", "Remote", "Remote", "Contract", "$30 / hr")));
        var jobId = (await posting.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var apply = await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/jobs/{jobId}/apply", seeker.AccessToken));

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var dashboard = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", employer.AccessToken));
        var body = await dashboard.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal("", Assert.Single(body!.Employer!.Candidates).Headline);
    }

    private static async Task<JobSeekerDashboard> GetJobSeekerDashboardAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/dashboard", token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>())!.JobSeeker!;
    }
}
