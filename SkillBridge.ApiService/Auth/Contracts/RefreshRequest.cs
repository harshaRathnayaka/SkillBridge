namespace SkillBridge.ApiService.Auth.Contracts;

public record RefreshRequest(string RefreshToken, string DeviceId);
