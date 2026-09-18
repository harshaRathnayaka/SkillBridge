namespace SkillBridge.ApiService.Data;

// Snapshot values seeded once at registration for numbers that don't have a real underlying
// subsystem yet (reviews, payments, hiring analytics) — deliberately not computed. Building
// those subsystems just to power one dashboard stat would be over-engineering for a read-only
// pass; only the fields relevant to the user's own role are ever populated.
public class UserProfile
{
    public required string UserId { get; set; }
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }
    public string? EarningsLabel { get; set; }
    public decimal? EarningsTrendPercent { get; set; }
    public int? HoursThisMonth { get; set; }
    public int? TimeToHireDaysMedian { get; set; }
    public int? ProfileStrengthPercent { get; set; }
    public string? ProfileStrengthNote { get; set; }
    public int? SavedRoles { get; set; }
}
