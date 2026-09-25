namespace SkillBridge.ApiService.Courses.Contracts;

public record CourseCatalogItem(
    Guid Id,
    string TeacherName,
    string Title,
    string Mode,
    string PriceLabel,
    decimal RatingAverage,
    DateTimeOffset? NextSessionAt,
    bool IsEnrolled);
