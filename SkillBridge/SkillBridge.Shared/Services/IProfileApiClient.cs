namespace SkillBridge.Shared.Services;

// Mirrors SkillBridge.ApiService.Profiles.Contracts.{ProfileResponse,ProfileSkillItem,ProfileReferenceItem},
// same duplicated-DTO convention the other API clients use.
public record ProfileSkillInfo(Guid Id, string Name);

public record ProfileReferenceInfo(Guid Id, string Name, string Relationship, string? Contact, string? Comment);

public record ProfileInfo(
    string Headline, string Bio,
    IReadOnlyList<ProfileSkillInfo> Skills, IReadOnlyList<ProfileReferenceInfo> References,
    int StrengthPercent, string? StrengthNote);

// Client for the signed-in user's own profile (/api/profile). Writes reuse MarketplaceApiResult;
// callers reload the profile afterward because every write can change the strength score.
public interface IProfileApiClient
{
    Task<ProfileInfo?> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> UpdateProfileAsync(
        string accessToken, string headline, string bio, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> AddSkillAsync(string accessToken, string name, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> RemoveSkillAsync(string accessToken, Guid skillId, CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> AddReferenceAsync(
        string accessToken, string name, string relationship, string? contact, string? comment,
        CancellationToken cancellationToken = default);

    Task<MarketplaceApiResult> RemoveReferenceAsync(string accessToken, Guid referenceId, CancellationToken cancellationToken = default);
}
