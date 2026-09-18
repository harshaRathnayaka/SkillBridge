namespace SkillBridge.ApiService.Auth.Contracts;

public record ConfirmEmailRequest(string Email, string Token);
