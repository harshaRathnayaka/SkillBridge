using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace SkillBridge.Shared.Services;

// Talks to SkillBridge.ApiService's /api/dashboard endpoint. Same shape and same
// NavigationManager-fallback reasoning as AuthApiClient — see that class's comment.
public class DashboardApiClient(HttpClient httpClient, NavigationManager? navigationManager = null) : IDashboardApiClient
{
    private HttpClient Client
    {
        get
        {
            httpClient.BaseAddress ??= navigationManager is not null ? new Uri(navigationManager.BaseUri) : null;
            return httpClient;
        }
    }

    public async Task<DashboardInfo?> GetDashboardAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DashboardInfo>(cancellationToken: cancellationToken);
    }
}
