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
}
