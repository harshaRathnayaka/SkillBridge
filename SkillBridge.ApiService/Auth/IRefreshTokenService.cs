namespace SkillBridge.ApiService.Auth;

public interface IRefreshTokenService
{
    // Issues a brand-new refresh token for this (userId, deviceId) pair, revoking any
    // prior active token for the SAME device only — other devices' tokens are untouched,
    // which is what lets a user stay logged in on mobile and web simultaneously.
    Task<string> IssueAsync(
        string userId,
        string deviceId,
        string? deviceLabel,
        DateTimeOffset expiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default);

    // Atomically rotates a refresh token: the presented raw token is revoked and replaced
    // by a new one for the same (userId, deviceId). Fails (Succeeded = false) if the token
    // is unknown, expired, or already revoked — the last case also revokes every other
    // still-active token for that device (reuse-detection response to a possible replay),
    // and is also what a losing request in a genuine concurrent-refresh race observes.
    Task<RefreshRotationResult> RotateAsync(
        string rawToken,
        string deviceId,
        DateTimeOffset newExpiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default);

    // Revokes just the token's own (userId, deviceId) session. A no-op if the token is
    // unknown/already revoked — logout doesn't need to distinguish those from "already logged out".
    Task RevokeAsync(string rawToken, string deviceId, CancellationToken cancellationToken = default);
}

public record RefreshRotationResult(bool Succeeded, string? UserId = null, string? NewRawToken = null);
