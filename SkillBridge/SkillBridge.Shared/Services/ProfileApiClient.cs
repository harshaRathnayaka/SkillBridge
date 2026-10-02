using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace SkillBridge.Shared.Services;

// Talks to SkillBridge.ApiService's /api/profile endpoints. Same shape and same
// NavigationManager-fallback reasoning as AuthApiClient/MarketplaceApiClient — see AuthApiClient's
// comment.
public class ProfileApiClient(HttpClient httpClient, NavigationManager? navigationManager = null) : IProfileApiClient
{
    private HttpClient Client
    {
        get
        {
            httpClient.BaseAddress ??= navigationManager is not null ? new Uri(navigationManager.BaseUri) : null;
            return httpClient;
        }
    }

    private record UpdateProfileBody(string Headline, string Bio);
    private record AddSkillBody(string Name);
    private record AddReferenceBody(string Name, string Relationship, string? Contact, string? Comment);
    private record ErrorBody(string[] Errors);

    public async Task<ProfileInfo?> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProfileInfo>(cancellationToken: cancellationToken)
            : null;
    }

    public Task<MarketplaceApiResult> UpdateProfileAsync(
        string accessToken, string headline, string bio, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, "/api/profile", accessToken, new UpdateProfileBody(headline, bio), cancellationToken);

    public Task<MarketplaceApiResult> AddSkillAsync(string accessToken, string name, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "/api/profile/skills", accessToken, new AddSkillBody(name), cancellationToken);

    public Task<MarketplaceApiResult> RemoveSkillAsync(string accessToken, Guid skillId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"/api/profile/skills/{skillId}", accessToken, null, cancellationToken);

    public Task<MarketplaceApiResult> AddReferenceAsync(
        string accessToken, string name, string relationship, string? contact, string? comment,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "/api/profile/references", accessToken,
            new AddReferenceBody(name, relationship, contact, comment), cancellationToken);

    public Task<MarketplaceApiResult> RemoveReferenceAsync(string accessToken, Guid referenceId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"/api/profile/references/{referenceId}", accessToken, null, cancellationToken);

    private async Task<MarketplaceApiResult> SendAsync<TBody>(
        HttpMethod method, string url, string accessToken, TBody? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return MarketplaceApiResult.Success();
        }

        ErrorBody? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            // Empty/non-JSON error bodies (e.g. a bare 404) fall through to the generic message.
        }
        return MarketplaceApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }
}
