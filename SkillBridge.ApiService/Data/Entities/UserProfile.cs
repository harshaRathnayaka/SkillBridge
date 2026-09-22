namespace SkillBridge.ApiService.Data;

// Snapshot values for numbers that don't have a real underlying subsystem yet (reviews,
// payments, hiring analytics). Nothing currently writes these — building those subsystems just
// to populate one dashboard stat would be over-engineering — so every row is effectively empty
// today and DashboardEndpoints' null-coalescing shows "—"/0 accordingly. The table exists so a
// future real feature (e.g. an actual reviews or payments system) has somewhere to write to
// without a schema change.
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
