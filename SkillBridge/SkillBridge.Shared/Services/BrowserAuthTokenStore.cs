using Microsoft.JSInterop;

namespace SkillBridge.Shared.Services;

// Used by both Web (Server) and Web.Client (WASM) — identical either way because IJSRuntime
// works the same from a component's point of view under both hosting models.
//
// Stores tokens in localStorage (not a cookie). A cookie-based BFF was tried first, but Blazor
// Server's interactive circuit has no live HttpContext to call SignInAsync from once the
// initial request has completed — cookie auth only works there via a real full-page HTML form
// POST, which would mean Login/Register couldn't stay as shared EditForm components across
// Web and MAUI. localStorage keeps one implementation working uniformly across all three
// hosts, at the accepted cost of the refresh token being JS-reachable in the browser — bounded
// by the token's own rotation-with-reuse-detection design (see IRefreshTokenService) rather
// than by storage isolation.
public class BrowserAuthTokenStore(IJSRuntime js) : IAuthTokenStore
{
    private const string AccessTokenKey = "sb_access_token";
    private const string RefreshTokenKey = "sb_refresh_token";
    private const string ExpiresAtKey = "sb_expires_at";
    private const string DeviceIdKey = "sb_device_id";
    private const string ActiveRoleKey = "sb_active_role";

    public async Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        await js.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
        await js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
        await js.InvokeVoidAsync("localStorage.setItem", ExpiresAtKey, expiresAtUtc.ToString("O"));
    }

    public async Task<StoredTokens?> GetTokensAsync()
    {
        var accessToken = await js.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);
        var refreshToken = await js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
        var expiresAtRaw = await js.InvokeAsync<string?>("localStorage.getItem", ExpiresAtKey);
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(expiresAtRaw))
        {
            return null;
        }

        return new StoredTokens(accessToken, refreshToken, DateTimeOffset.Parse(expiresAtRaw));
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", ExpiresAtKey);
        // Not DeviceIdKey — that persists across logout by design. The active role is reset,
        // though: a different account signing in on the same browser shouldn't inherit a
        // preference for a role it might not even hold.
        await js.InvokeVoidAsync("localStorage.removeItem", ActiveRoleKey);
    }

    public async Task<string?> GetActiveRoleAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", ActiveRoleKey);

    public async Task SetActiveRoleAsync(string role) =>
        await js.InvokeVoidAsync("localStorage.setItem", ActiveRoleKey, role);

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        var existing = await js.InvokeAsync<string?>("localStorage.getItem", DeviceIdKey);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var deviceId = Guid.NewGuid().ToString();
        await js.InvokeVoidAsync("localStorage.setItem", DeviceIdKey, deviceId);
        return deviceId;
    }
}
