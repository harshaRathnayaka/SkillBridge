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

// Mirrors SkillBridge.ApiService.Courses.Contracts.CourseCatalogItem /
// SkillBridge.ApiService.Jobs.Contracts.{JobCatalogItem,EmployerJobListingItem}, same
// duplicated-DTO convention IDashboardApiClient already uses for the dashboard payload.
public record CourseCatalogItemInfo(
    Guid Id, string TeacherName, string Title, string Mode, string PriceLabel,
    decimal RatingAverage, DateTimeOffset? NextSessionAt, bool IsEnrolled);

public record JobCatalogItemInfo(
    Guid Id, string Title, string CompanyDisplayName, string Location, string WorkMode,
    string EmploymentType, string RateLabel, string PostedLabel, int ApplicantCount, bool IsApplied);

public record EmployerJobListingItemInfo(
    Guid Id, string Title, string Location, string WorkMode, string EmploymentType,
    string PostedLabel, int ApplicantCount);

public interface IMarketplaceApiClient
{
    Task<MarketplaceApiResult> CreateCourseAsync(
        string accessToken, string title, string mode, decimal priceAmount, string currency,
        string? priceUnitLabel, DateTimeOffset? nextSessionAt, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> EnrollAsync(string accessToken, Guid courseId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> CreateMaterialAsync(
        string accessToken, Guid courseId, string title, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> PublishMaterialAsync(string accessToken, Guid materialId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> UnpublishMaterialAsync(string accessToken, Guid materialId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> CreateJobPostingAsync(
        string accessToken, string title, string location, string workMode, string employmentType,
        string rateLabel, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> ApplyAsync(string accessToken, Guid jobPostingId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> AdvanceApplicationAsync(string accessToken, Guid applicationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourseCatalogItemInfo>> GetCourseCatalogAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobCatalogItemInfo>> GetJobCatalogAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployerJobListingItemInfo>> GetMyJobPostingsAsync(string accessToken, CancellationToken cancellationToken = default);
}
