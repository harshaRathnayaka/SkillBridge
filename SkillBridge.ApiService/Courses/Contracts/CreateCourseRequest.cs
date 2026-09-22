namespace SkillBridge.ApiService.Courses.Contracts;

public record CreateCourseRequest(
    string Title,
    string Mode,
    decimal PriceAmount,
    string Currency,
    string? PriceUnitLabel,
    DateTimeOffset? NextSessionAt);
