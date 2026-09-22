namespace SkillBridge.Shared.Services;

// Write-side counterpart to IDashboardApiClient — the "basic functionality" actions behind
// each role's dashboard (create a course, enroll, post a job, apply, advance a candidate).
// All return a simple success/errors result; callers reload the dashboard afterward rather
// than trying to patch it in place, since a single action can affect several stat tiles at once.
public record MarketplaceApiResult(bool Succeeded, string[] Errors)
{
    public static MarketplaceApiResult Success() => new(true, []);
    public static MarketplaceApiResult Failure(string[] errors) => new(false, errors);
}

public interface IMarketplaceApiClient
{
    Task<MarketplaceApiResult> CreateCourseAsync(
        string accessToken, string title, string mode, decimal priceAmount, string currency,
        string? priceUnitLabel, DateTimeOffset? nextSessionAt, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> EnrollAsync(string accessToken, Guid courseId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> CreateMaterialAsync(
        string accessToken, Guid courseId, string title, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> PublishMaterialAsync(string accessToken, Guid materialId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> CreateJobPostingAsync(
        string accessToken, string title, string location, string workMode, string employmentType,
        string rateLabel, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> ApplyAsync(string accessToken, Guid jobPostingId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> AdvanceApplicationAsync(string accessToken, Guid applicationId, CancellationToken cancellationToken = default);
}
