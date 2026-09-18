using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Data;

namespace SkillBridge.ApiService.Auth;

public class RefreshTokenService(ApplicationDbContext db) : IRefreshTokenService
{
    public async Task<string> IssueAsync(
        string userId,
        string deviceId,
        string? deviceLabel,
        DateTimeOffset expiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.DeviceId == deviceId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in existing)
        {
            Revoke(token);
        }

        var rawToken = GenerateRawToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            DeviceLabel = deviceLabel,
            TokenHash = Hash(rawToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAtUtc,
            CreatedByIp = createdByIp,
        });

        // This call shares its DbContext with the UserManager calls earlier in the same
        // request (e.g. lockout bookkeeping on ApplicationUser) — under heavy concurrent
        // login load against the same account, that can trip an unrelated optimistic-
        // concurrency conflict on this SaveChanges call even though this device's own new
        // row isn't itself contended. A login that already passed credential checks
        // shouldn't fail because of that, so this retries (reloading whatever tripped the
        // check) rather than surfacing a 500 for something the caller can't act on.
        await SaveWithRetryAsync(cancellationToken);
        return rawToken;
    }

    private async Task SaveWithRetryAsync(CancellationToken cancellationToken, int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // Transient (e.g. a SQLite write-lock timeout under contention) — just retry.
            }
        }
    }

    public async Task<RefreshRotationResult> RotateAsync(
        string rawToken,
        string deviceId,
        DateTimeOffset newExpiresAtUtc,
        string? createdByIp,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawToken);
        var current = await db.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash && rt.DeviceId == deviceId, cancellationToken);

        if (current is null)
        {
            return new RefreshRotationResult(Succeeded: false);
        }

        if (current.RevokedAt is not null)
        {
            // Presenting an already-rotated token is either token theft/replay, or the losing
            // side of a genuine concurrent-refresh race. Either way, the safe response is to
            // revoke the whole family for this device so a stale/stolen token can't be reused.
            await RevokeActiveFamilyAsync(current.UserId, deviceId, cancellationToken);
            return new RefreshRotationResult(Succeeded: false);
        }

        if (current.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new RefreshRotationResult(Succeeded: false);
        }

        var newRawToken = GenerateRawToken();
        var newRow = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = current.UserId,
            DeviceId = deviceId,
            DeviceLabel = current.DeviceLabel,
            TokenHash = Hash(newRawToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = newExpiresAtUtc,
            CreatedByIp = createdByIp,
        };

        Revoke(current);
        current.ReplacedByTokenId = newRow.Id;
        db.RefreshTokens.Add(newRow);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Covers both the expected optimistic-concurrency conflict (another request
            // rotated this exact row between our read and write — we lost the race, and the
            // winner's rotation already stands) and a transient SQLite write-lock timeout
            // under contention. Either way the safe response is to reject this attempt rather
            // than risk double-issuing a token pair.
            return new RefreshRotationResult(Succeeded: false);
        }

        return new RefreshRotationResult(Succeeded: true, current.UserId, newRawToken);
    }

    public async Task RevokeAsync(string rawToken, string deviceId, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawToken);
        var current = await db.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash && rt.DeviceId == deviceId && rt.RevokedAt == null, cancellationToken);

        if (current is null)
        {
            return;
        }

        Revoke(current);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeActiveFamilyAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        var activeRows = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.DeviceId == deviceId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var row in activeRows)
        {
            Revoke(row);
        }

        if (activeRows.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void Revoke(RefreshToken token)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ConcurrencyStamp = Guid.NewGuid().ToString();
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    internal static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
