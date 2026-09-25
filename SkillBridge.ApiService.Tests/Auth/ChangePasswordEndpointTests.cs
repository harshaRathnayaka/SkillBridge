using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class ChangePasswordEndpointTests
{
    private const string OriginalPassword = "P@ssw0rd123!";
    private const string NewPassword = "N3wP@ssword456!";

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, OriginalPassword, "Change Pw User", ["Student"], "device-1"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static HttpRequestMessage BuildRequest(string? accessToken, ChangePasswordRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(body),
        };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    [Fact]
    public async Task Change_password_with_the_correct_current_password_succeeds_and_the_new_password_works()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "change-pw-ok@example.com";
        var auth = await RegisterAsync(client, email);

        var response = await client.SendAsync(
            BuildRequest(auth.AccessToken, new ChangePasswordRequest(OriginalPassword, NewPassword)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, OriginalPassword, "device-2"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, NewPassword, "device-2"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Change_password_without_a_bearer_token_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "change-pw-noauth@example.com";
        await RegisterAsync(client, email);

        var response = await client.SendAsync(
            BuildRequest(null, new ChangePasswordRequest(OriginalPassword, NewPassword)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_an_invalid_bearer_token_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "change-pw-badtoken@example.com";
        await RegisterAsync(client, email);

        var response = await client.SendAsync(
            BuildRequest("not-a-real-jwt", new ChangePasswordRequest(OriginalPassword, NewPassword)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_the_wrong_current_password_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "change-pw-wrongcurrent@example.com";
        var auth = await RegisterAsync(client, email);

        var response = await client.SendAsync(
            BuildRequest(auth.AccessToken, new ChangePasswordRequest("NotTheRightPassword1!", NewPassword)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_to_a_weak_password_is_rejected()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "change-pw-weak@example.com";
        var auth = await RegisterAsync(client, email);

        var response = await client.SendAsync(
            BuildRequest(auth.AccessToken, new ChangePasswordRequest(OriginalPassword, "weak")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_users_own_token_cannot_be_used_to_change_a_different_users_password()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        var authA = await RegisterAsync(client, "change-pw-user-a@example.com");
        await RegisterAsync(client, "change-pw-user-b@example.com");

        // Even with a valid token (for user A), change-password only ever acts on the caller
        // identified by that token — there's no way to target a different user's account.
        var response = await client.SendAsync(
            BuildRequest(authA.AccessToken, new ChangePasswordRequest(OriginalPassword, NewPassword)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var userBStillWorks = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("change-pw-user-b@example.com", OriginalPassword, "device-1"));
        Assert.Equal(HttpStatusCode.OK, userBStillWorks.StatusCode);
    }
}
