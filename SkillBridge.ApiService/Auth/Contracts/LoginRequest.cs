namespace SkillBridge.ApiService.Auth.Contracts;

public record LoginRequest(
    string Email,
    string Password,
    string DeviceId,
    string? DeviceLabel = null,
    bool RememberMe = false);
