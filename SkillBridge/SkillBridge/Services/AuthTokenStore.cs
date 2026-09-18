using SkillBridge.Shared.Services;

namespace SkillBridge.Services;

// MAUI has no browser/XSS surface, so unlike the Web heads, tokens are stored directly via
// SecureStorage (OS keychain / Android Keystore) rather than needing a BFF cookie indirection.
public class AuthTokenStore : IAuthTokenStore
{
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string ExpiresAtKey = "auth.expires_at";
    private const string DeviceIdKey = "auth.device_id";

    public async Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, expiresAtUtc.ToString("O"));
    }

    public async Task<StoredTokens?> GetTokensAsync()
    {
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        var expiresAtRaw = await SecureStorage.Default.GetAsync(ExpiresAtKey);

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(expiresAtRaw))
        {
            return null;
        }

        return new StoredTokens(accessToken, refreshToken, DateTimeOffset.Parse(expiresAtRaw));
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        return Task.CompletedTask;
    }

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        var existing = await SecureStorage.Default.GetAsync(DeviceIdKey);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var deviceId = Guid.NewGuid().ToString();
        await SecureStorage.Default.SetAsync(DeviceIdKey, deviceId);
        return deviceId;
    }
}
