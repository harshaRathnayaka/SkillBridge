namespace SkillBridge.Shared.Services;

public record StoredTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

// One interface, one implementation per platform (mirrors IFormFactor): MAUI stores both
// tokens in SecureStorage. The Web hosts (Server + WASM) share a BrowserAuthTokenStore that
// keeps both tokens in localStorage — see that class for why a cookie-based approach isn't
// viable given Blazor Server's interactive-circuit lifecycle.
public interface IAuthTokenStore
{
    Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc);

    Task<StoredTokens?> GetTokensAsync();

    Task ClearAsync();

    Task<string> GetOrCreateDeviceIdAsync();

    // Convenience wrapper around GetTokensAsync for the common "just need the access token, or
    // null if signed out" case — a default method needs no changes to either platform
    // implementation (BrowserAuthTokenStore, AuthTokenStore).
    async Task<string?> GetAccessTokenAsync() => (await GetTokensAsync())?.AccessToken;

    // Which of a multi-role account's roles is currently "active" — drives NavMenu's links and
    // RoleDashboard's data, same idea as the Lovable prototype's own demo role-switcher but
    // backed by roles the account genuinely holds. A per-viewer UI preference, not part of the
    // JWT/auth state, so it lives alongside the tokens rather than on the server.
    Task<string?> GetActiveRoleAsync();

    Task SetActiveRoleAsync(string role);
}
