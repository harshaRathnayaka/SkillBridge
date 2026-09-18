using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

public class LogoutEndpointTests
{
    [Fact]
    public async Task Logout_revokes_only_that_devices_session_and_leaves_other_devices_signed_in()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();
        const string email = "logout@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, "P@ssw0rd123!", "Logout User", "Student", "web-1"));
        var mobileLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", "mobile-1"));
        var mobileTokens = await mobileLogin.Content.ReadFromJsonAsync<AuthResponse>();

        var webLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "P@ssw0rd123!", "web-1"));
        var webTokens = await webLogin.Content.ReadFromJsonAsync<AuthResponse>();

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(webTokens!.RefreshToken, "web-1"));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var webRefreshAfterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(webTokens.RefreshToken, "web-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, webRefreshAfterLogout.StatusCode);

        var mobileRefreshStillWorks = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(mobileTokens!.RefreshToken, "mobile-1"));
        Assert.Equal(HttpStatusCode.OK, mobileRefreshStillWorks.StatusCode);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_is_a_no_op_204()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest("not-a-real-token", "device-x"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
