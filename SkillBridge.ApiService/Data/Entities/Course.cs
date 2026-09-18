namespace SkillBridge.ApiService.Data;

public enum CourseMode
{
    Live,
    SelfPaced,
    DraftSlots,
}

// TeacherName is a denormalized copy of the owning teacher's display name at seed time, not a
// live join to ApplicationUser — this table also holds a small fixed demo catalog "taught" by
// non-account system teachers (see DashboardContentSeeder), so there isn't always a real user
// row to join to.
public class Course
{
    public Guid Id { get; set; }
    public required string TeacherId { get; set; }
    public required string TeacherName { get; set; }
    public required string Title { get; set; }
    public CourseMode Mode { get; set; }
    public decimal PriceAmount { get; set; }
    public required string Currency { get; set; }
    public string? PriceUnitLabel { get; set; }
    public decimal RatingAverage { get; set; }
    public DateTimeOffset? NextSessionAt { get; set; }
}
