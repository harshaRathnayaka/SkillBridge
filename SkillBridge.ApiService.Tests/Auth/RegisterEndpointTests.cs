using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class RegisterEndpointTests
{
    [Fact]
    public async Task Register_with_valid_data_returns_tokens_with_role_claim()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            Email: "teacher1@example.com",
            Password: "P@ssw0rd123!",
            DisplayName: "Ada Teacher",
            Roles: ["Teacher"],
            DeviceId: "device-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal(["Teacher"], body.Roles);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Teacher");
    }

    [Fact]
    public async Task Register_with_several_roles_grants_all_of_them_and_each_is_usable()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "multi-role@example.com", "P@ssw0rd123!", "Multi Role", ["Teacher", "Student"], "device-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(["Teacher", "Student"], body!.Roles.OrderDescending());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        var roleClaims = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Teacher", roleClaims);
        Assert.Contains("Student", roleClaims);
    }

    [Fact]
    public async Task Register_with_no_roles_selected_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "noroles@example.com", "P@ssw0rd123!", "Someone", [], "device-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var request = new RegisterRequest("dup@example.com", "P@ssw0rd123!", "First", ["Student"], "device-1");
        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/auth/register", request with { DeviceId = "device-2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_unknown_role_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "unknownrole@example.com", "P@ssw0rd123!", "Someone", ["Wizard"], "device-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_one_unknown_role_among_valid_ones_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "partlyunknownrole@example.com", "P@ssw0rd123!", "Someone", ["Student", "Wizard"], "device-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_400()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "weakpw@example.com", "123", "Someone", ["Student"], "device-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
