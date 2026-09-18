namespace SkillBridge.ApiService.Data;

public class RefreshToken
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string TokenHash { get; set; }
    public required string DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    // App-managed concurrency token (regenerated on every update), mirroring the same
    // pattern Identity itself uses for ApplicationUser.ConcurrencyStamp — chosen over a
    // DB-generated rowversion column since SQLite has no native equivalent.
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
