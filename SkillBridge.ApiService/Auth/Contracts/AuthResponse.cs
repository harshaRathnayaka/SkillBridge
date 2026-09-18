namespace SkillBridge.ApiService.Auth.Contracts;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string[] Roles,
    string DisplayName);
