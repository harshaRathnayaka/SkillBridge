namespace SkillBridge.ApiService.Data;

// One row per user, created lazily the first time they save their profile (registration does not
// create one). Headline/Bio are real, user-written profile content. The remaining columns are
// snapshot values for numbers that don't have a real underlying subsystem yet (reviews, payments,
// hiring analytics) — nothing writes those, so DashboardEndpoints' null-coalescing shows "—"/0.
// Profile strength is deliberately not stored here: it is computed from the profile's contents
// (see Profiles/ProfileStrength.cs), so it can never drift out of date.
public class UserProfile
{
    public required string UserId { get; set; }
    public string Headline { get; set; } = "";
    public string Bio { get; set; } = "";
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }
    public string? EarningsLabel { get; set; }
    public decimal? EarningsTrendPercent { get; set; }
    public int? HoursThisMonth { get; set; }
    public int? TimeToHireDaysMedian { get; set; }
    public int? SavedRoles { get; set; }
}
