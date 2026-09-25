using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace SkillBridge.Shared.Services;

// Talks to SkillBridge.ApiService's /api/courses and /api/jobs endpoints. Same shape and same
// NavigationManager-fallback reasoning as AuthApiClient/DashboardApiClient — see AuthApiClient's
// comment.
public class MarketplaceApiClient(HttpClient httpClient, NavigationManager? navigationManager = null) : IMarketplaceApiClient
{
    private HttpClient Client
    {
        get
        {
            httpClient.BaseAddress ??= navigationManager is not null ? new Uri(navigationManager.BaseUri) : null;
            return httpClient;
        }
    }

    private record CreateCourseBody(string Title, string Mode, decimal PriceAmount, string Currency, string? PriceUnitLabel, DateTimeOffset? NextSessionAt);
    private record CreateMaterialBody(string Title);
    private record CreateJobPostingBody(string Title, string Location, string WorkMode, string EmploymentType, string RateLabel);
    private record ErrorBody(string[] Errors);

    public Task<MarketplaceApiResult> CreateCourseAsync(
        string accessToken, string title, string mode, decimal priceAmount, string currency,
        string? priceUnitLabel, DateTimeOffset? nextSessionAt, CancellationToken cancellationToken = default) =>
        PostAsync("/api/courses", accessToken,
            new CreateCourseBody(title, mode, priceAmount, currency, priceUnitLabel, nextSessionAt), cancellationToken);

    public Task<MarketplaceApiResult> EnrollAsync(string accessToken, Guid courseId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"/api/courses/{courseId}/enroll", accessToken, null, cancellationToken);

    public Task<MarketplaceApiResult> CreateMaterialAsync(
        string accessToken, Guid courseId, string title, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/courses/{courseId}/materials", accessToken, new CreateMaterialBody(title), cancellationToken);

    public Task<MarketplaceApiResult> PublishMaterialAsync(string accessToken, Guid materialId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"/api/courses/materials/{materialId}/publish", accessToken, null, cancellationToken);

    public Task<MarketplaceApiResult> UnpublishMaterialAsync(string accessToken, Guid materialId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"/api/courses/materials/{materialId}/unpublish", accessToken, null, cancellationToken);

    public Task<MarketplaceApiResult> CreateJobPostingAsync(
        string accessToken, string title, string location, string workMode, string employmentType,
        string rateLabel, CancellationToken cancellationToken = default) =>
        PostAsync("/api/jobs", accessToken, new CreateJobPostingBody(title, location, workMode, employmentType, rateLabel), cancellationToken);

    public Task<MarketplaceApiResult> ApplyAsync(string accessToken, Guid jobPostingId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"/api/jobs/{jobPostingId}/apply", accessToken, null, cancellationToken);

    public Task<MarketplaceApiResult> AdvanceApplicationAsync(string accessToken, Guid applicationId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"/api/jobs/applications/{applicationId}/advance", accessToken, null, cancellationToken);

    public Task<IReadOnlyList<CourseCatalogItemInfo>> GetCourseCatalogAsync(string accessToken, CancellationToken cancellationToken = default) =>
        GetAsync<CourseCatalogItemInfo>("/api/courses", accessToken, cancellationToken);

    public Task<IReadOnlyList<JobCatalogItemInfo>> GetJobCatalogAsync(string accessToken, CancellationToken cancellationToken = default) =>
        GetAsync<JobCatalogItemInfo>("/api/jobs", accessToken, cancellationToken);

    public Task<IReadOnlyList<EmployerJobListingItemInfo>> GetMyJobPostingsAsync(string accessToken, CancellationToken cancellationToken = default) =>
        GetAsync<EmployerJobListingItemInfo>("/api/jobs/mine", accessToken, cancellationToken);

    private async Task<IReadOnlyList<T>> GetAsync<T>(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var items = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken: cancellationToken);
        return items ?? [];
    }

    private async Task<MarketplaceApiResult> PostAsync<TBody>(string url, string accessToken, TBody? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
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

        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: cancellationToken);
        return MarketplaceApiResult.Failure(error?.Errors ?? ["Something went wrong. Please try again."]);
    }
}
