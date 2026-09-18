namespace SkillBridge.ApiService.Auth.Contracts;

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
