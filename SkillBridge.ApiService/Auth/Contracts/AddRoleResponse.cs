namespace SkillBridge.ApiService.Auth.Contracts;

// No refresh token here (unlike AuthResponse) — adding a role only changes what's baked into
// the short-lived access token; the caller's existing refresh token is untouched and still valid.
public record AddRoleResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string[] Roles);
