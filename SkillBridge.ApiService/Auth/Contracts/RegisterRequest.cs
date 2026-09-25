namespace SkillBridge.ApiService.Auth.Contracts;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string DeviceId,
    string? DeviceLabel = null);
