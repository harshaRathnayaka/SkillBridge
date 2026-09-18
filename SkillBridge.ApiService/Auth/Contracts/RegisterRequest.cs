namespace SkillBridge.ApiService.Auth.Contracts;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role,
    string DeviceId,
    string? DeviceLabel = null);
