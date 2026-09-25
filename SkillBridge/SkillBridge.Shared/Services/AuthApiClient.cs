using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace SkillBridge.Shared.Services;

// Talks to SkillBridge.ApiService's /api/auth endpoints. Identical for every host (Web,
// Web.Client, MAUI) — only the injected HttpClient's BaseAddress differs per host, and only
// where the resulting tokens get stored differs (see IAuthTokenStore), so this is registered
// once and shared rather than duplicated per platform.
//
// NavigationManager is only used as a fallback for the Web (Server) host, which needs the
// current request's dynamic base URI. It's taken as a constructor parameter (resolved in the
// caller's own DI scope) rather than set via AddHttpClient's configureClient callback, because
// that callback runs through IHttpClientFactory's internal handler-pooling provider, which is
// rooted and cannot resolve scoped services like NavigationManager. MAUI and Web.Client
// already set BaseAddress directly in their own AddHttpClient registration, so for them this
// is never null and NavigationManager (if even registered) is simply unused.
public class AuthApiClient(HttpClient httpClient, NavigationManager? navigationManager = null) : IAuthApiClient
{
    private HttpClient Client
    {
        get
        {
            httpClient.BaseAddress ??= navigationManager is not null ? new Uri(navigationManager.BaseUri) : null;
            return httpClient;
        }
    }

    private record RegisterBody(string Email, string Password, string DisplayName, IReadOnlyList<string> Roles, string DeviceId, string? DeviceLabel);
    private record LoginBody(string Email, string Password, string DeviceId, string? DeviceLabel, bool RememberMe);
    private record RefreshBody(string RefreshToken, string DeviceId);
    private record ForgotPasswordBody(string Email);
    private record ResetPasswordBody(string Email, string Token, string NewPassword);
    private record ChangePasswordBody(string CurrentPassword, string NewPassword);
    private record AddRoleBody(string Role);
    private record ErrorBody(string[] Errors);

    public Task<AuthApiResult> RegisterAsync(
        string email, string password, string displayName, IReadOnlyList<string> roles, string deviceId,
        string? deviceLabel = null, CancellationToken cancellationToken = default) =>
        PostAuthAsync("/api/auth/register", new RegisterBody(email, password, displayName, roles, deviceId, deviceLabel), cancellationToken);

    public Task<AuthApiResult> LoginAsync(
        string email, string password, string deviceId,
        string? deviceLabel = null, bool rememberMe = false, CancellationToken cancellationToken = default) =>
        PostAuthAsync("/api/auth/login", new LoginBody(email, password, deviceId, deviceLabel, rememberMe), cancellationToken);

    public Task<AuthApiResult> RefreshAsync(string refreshToken, string deviceId, CancellationToken cancellationToken = default) =>
        PostAuthAsync("/api/auth/refresh", new RefreshBody(refreshToken, deviceId), cancellationToken);

    public async Task LogoutAsync(string refreshToken, string deviceId, CancellationToken cancellationToken = default)
    {
        await Client.PostAsJsonAsync("/api/auth/logout", new RefreshBody(refreshToken, deviceId), cancellationToken);
    }

    public Task<SimpleApiResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default) =>
        PostSimpleAsync("/api/auth/forgot-password", new ForgotPasswordBody(email), cancellationToken);

    public Task<SimpleApiResult> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default) =>
        PostSimpleAsync("/api/auth/reset-password", new ResetPasswordBody(email, token, newPassword), cancellationToken);

    public async Task<SimpleApiResult> ChangePasswordAsync(
        string accessToken, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordBody(currentPassword, newPassword)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return SimpleApiResult.Success();
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        return SimpleApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }

    public async Task<AddRoleApiResult> AddRoleAsync(string accessToken, string role, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/roles")
        {
            Content = JsonContent.Create(new AddRoleBody(role)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<AddRolePayload>(cancellationToken: cancellationToken);
            return payload is null ? AddRoleApiResult.Failure(["Unexpected empty response."]) : AddRoleApiResult.Success(payload);
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        return AddRoleApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }

    private async Task<AuthApiResult> PostAuthAsync<TBody>(string url, TBody body, CancellationToken cancellationToken)
    {
        var response = await Client.PostAsJsonAsync(url, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<AuthPayload>(cancellationToken: cancellationToken);
            return payload is null ? AuthApiResult.Failure(["Unexpected empty response."]) : AuthApiResult.Success(payload);
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        return AuthApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }

    private async Task<SimpleApiResult> PostSimpleAsync<TBody>(string url, TBody body, CancellationToken cancellationToken)
    {
        var response = await Client.PostAsJsonAsync(url, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return SimpleApiResult.Success();
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        return SimpleApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }
}
