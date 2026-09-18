namespace SkillBridge.ApiService.Auth.Contracts;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
