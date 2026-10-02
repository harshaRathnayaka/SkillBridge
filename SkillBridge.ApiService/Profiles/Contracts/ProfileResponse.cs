namespace SkillBridge.ApiService.Profiles.Contracts;

public record ProfileResponse(
    string Headline,
    string Bio,
    IReadOnlyList<ProfileSkillItem> Skills,
    IReadOnlyList<ProfileReferenceItem> References,
    int StrengthPercent,
    string? StrengthNote);

public record ProfileSkillItem(Guid Id, string Name);

public record ProfileReferenceItem(Guid Id, string Name, string Relationship, string? Contact, string? Comment);
