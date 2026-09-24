namespace SkillBridge.ApiService.Data;

public enum CourseMode
{
    Live,
    SelfPaced,
    DraftSlots,
}

// TeacherName is a denormalized copy of the owning teacher's display name at course-creation
// time, not a live join to ApplicationUser — same plain-FK convention RefreshToken.UserId
// already uses, kept here for consistency rather than a real join.
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
