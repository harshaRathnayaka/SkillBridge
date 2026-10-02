namespace SkillBridge.ApiService.Profiles.Contracts;

public record UpdateProfileRequest(string Headline, string Bio);

public record AddSkillRequest(string Name);

public record AddReferenceRequest(string Name, string Relationship, string? Contact = null, string? Comment = null);
