namespace SkillBridge.Shared.Services;

// Mirrors SkillBridge.ApiService.Dashboard.Contracts.DashboardResponse (and its nested
// records) field-for-field. Duplicated rather than shared via a common project, matching how
// AuthApiClient's AuthPayload already duplicates AuthResponse instead of referencing the
// ApiService project directly.
public record UpcomingSessionInfo(string Title, string WithName, DateTimeOffset At);

public record StudentCourseInfo(
    string Title, string TeacherName, string Mode, int LessonsRemaining,
    int ProgressPercent, string NextLessonTitle, DateTimeOffset? NextSessionAt);

public record SuggestedTeacherInfo(Guid CourseId, string Name, string Subject, string RateLabel);

public record StudentDashboardInfo(
    int ActiveCourses,
    int LessonsRemainingTotal,
    int DistinctTeachers,
    int? HoursThisMonth,
    int AvgProgressPercent,
    IReadOnlyList<StudentCourseInfo> Courses,
    IReadOnlyList<UpcomingSessionInfo> UpcomingSessions,
    IReadOnlyList<SuggestedTeacherInfo> SuggestedTeachers);

public record TutorCourseInfo(
    Guid Id, string Title, string Mode, int LearnersEnrolled, DateTimeOffset? NextSessionAt,
    decimal RatingAverage, string PriceLabel);

public record MaterialSummaryInfo(Guid Id, string Title, string CourseTitle, string Status);

public record TutorDashboardInfo(
    int LiveClasses,
    int LearnersEnrolledTotal,
    int PublishedMaterials,
    int DraftMaterials,
    string EarningsLabel,
    decimal? EarningsTrendPercent,
    decimal? RatingAverage,
    int? RatingCount,
    IReadOnlyList<TutorCourseInfo> Courses,
    IReadOnlyList<UpcomingSessionInfo> UpcomingSessions,
    IReadOnlyList<MaterialSummaryInfo> RecentMaterials);

public record CandidateInfo(Guid ApplicationId, string Name, string Headline, string Stage, string RateLabel, string AppliedLabel);

public record EmployerDashboardInfo(
    int OpenRoles,
    int PostedThisWeek,
    int Applicants,
    int NeedReview,
    int Interviews,
    int? TimeToHireDaysMedian,
    IReadOnlyList<CandidateInfo> Candidates,
    IReadOnlyList<UpcomingSessionInfo> UpcomingSessions);

public record JobMatchInfo(
    Guid JobPostingId, string Title, string Company, string Location, string WorkMode,
    string EmploymentType, string RateLabel, string PostedLabel);

public record MyApplicationInfo(string Title, string Company, string Stage, string AppliedLabel);

public record JobSeekerDashboardInfo(
    int NewMatchesThisWeek,
    int ApplicationsCount,
    int AtInterview,
    int SavedRoles,
    int? ProfileStrengthPercent,
    string? ProfileStrengthNote,
    IReadOnlyList<JobMatchInfo> Matches,
    IReadOnlyList<MyApplicationInfo> MyApplications);

public record DashboardInfo(
    string Role,
    IReadOnlyList<string> AllRoles,
    StudentDashboardInfo? Student,
    TutorDashboardInfo? Tutor,
    EmployerDashboardInfo? Employer,
    JobSeekerDashboardInfo? JobSeeker);

public interface IDashboardApiClient
{
    // activeRole selects which section a multi-role account sees — null lets the server fall
    // back to whichever of the caller's roles it finds first (see DashboardEndpoints' comment).
    Task<DashboardInfo?> GetDashboardAsync(string accessToken, string? activeRole = null, CancellationToken cancellationToken = default);
}
