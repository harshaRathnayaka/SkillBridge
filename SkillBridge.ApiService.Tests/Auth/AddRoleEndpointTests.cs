using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class AddRoleEndpointTests
{
    private static async Task<(AuthResponse Auth, string DeviceId)> RegisterAsync(HttpClient client, string email, IReadOnlyList<string> roles)
    {
        var deviceId = $"device-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Add Role User", roles, deviceId));
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<AuthResponse>())!, deviceId);
    }

    private static HttpRequestMessage BuildRequest(string accessToken, AddRoleRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/roles") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task Adding_a_role_without_auth_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/roles", new AddRoleRequest("Teacher"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_new_role_to_a_single_role_account_grants_it_and_a_follow_up_action_using_it_succeeds()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var (auth, _) = await RegisterAsync(client, "add-role-student@example.com", ["Student"]);

        var response = await client.SendAsync(BuildRequest(auth.AccessToken, new AddRoleRequest("Teacher")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AddRoleResponse>();
        Assert.NotNull(body);
        Assert.Equal(["Student", "Teacher"], body!.Roles.OrderBy(r => r));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        var roleClaims = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Student", roleClaims);
        Assert.Contains("Teacher", roleClaims);

        // The new access token is immediately usable for a Teacher-gated action.
        var createCourseRequest = new HttpRequestMessage(HttpMethod.Post, "/api/courses")
        {
            Content = JsonContent.Create(new
            {
                Title = "New Role Course",
                Mode = "Live",
                PriceAmount = 10m,
                Currency = "USD",
                PriceUnitLabel = " / hr",
                NextSessionAt = (DateTimeOffset?)null,
            }),
        };
        createCourseRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var createCourseResponse = await client.SendAsync(createCourseRequest);
        Assert.Equal(HttpStatusCode.OK, createCourseResponse.StatusCode);
    }

    [Fact]
    public async Task Adding_a_role_the_account_already_has_returns_409()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var (auth, _) = await RegisterAsync(client, "add-role-duplicate@example.com", ["Student"]);

        var response = await client.SendAsync(BuildRequest(auth.AccessToken, new AddRoleRequest("Student")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Adding_an_unrecognized_role_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var (auth, _) = await RegisterAsync(client, "add-role-unknown@example.com", ["Student"]);

        var response = await client.SendAsync(BuildRequest(auth.AccessToken, new AddRoleRequest("Wizard")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_role_leaves_the_existing_refresh_token_valid()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var (auth, deviceId) = await RegisterAsync(client, "add-role-refresh@example.com", ["Student"]);

        await client.SendAsync(BuildRequest(auth.AccessToken, new AddRoleRequest("Teacher")));

        // AddRoleAsync must not touch the refresh token — the same one issued at registration
        // still rotates successfully afterward.
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(auth.RefreshToken, deviceId));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }
}
