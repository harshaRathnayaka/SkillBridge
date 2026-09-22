namespace SkillBridge.ApiService.Dashboard.Contracts;

// Grouped in one file rather than one-type-per-file (the convention Auth/Contracts uses):
// these records only ever exist together as branches of a single response tree, so splitting
// them wouldn't make any of them independently reusable — just more files to open together.
public record DashboardResponse(
    string Role,
    StudentDashboard? Student,
    TutorDashboard? Tutor,
    EmployerDashboard? Employer,
    JobSeekerDashboard? JobSeeker);

public record UpcomingSession(string Title, string WithName, DateTimeOffset At);

public record StudentDashboard(
    int ActiveCourses,
    int LessonsRemainingTotal,
    int DistinctTeachers,
    int? HoursThisMonth,
    int AvgProgressPercent,
    IReadOnlyList<StudentCourse> Courses,
    IReadOnlyList<UpcomingSession> UpcomingSessions,
    IReadOnlyList<SuggestedTeacher> SuggestedTeachers);

public record StudentCourse(
    string Title, string TeacherName, string Mode, int LessonsRemaining,
    int ProgressPercent, string NextLessonTitle, DateTimeOffset? NextSessionAt);

public record SuggestedTeacher(Guid CourseId, string Name, string Subject, string RateLabel);

public record TutorDashboard(
    int LiveClasses,
    int LearnersEnrolledTotal,
    int PublishedMaterials,
    int DraftMaterials,
    string EarningsLabel,
    decimal? EarningsTrendPercent,
    decimal? RatingAverage,
    int? RatingCount,
    IReadOnlyList<TutorCourse> Courses,
    IReadOnlyList<UpcomingSession> UpcomingSessions,
    IReadOnlyList<MaterialSummary> RecentMaterials);

public record TutorCourse(
    Guid Id, string Title, string Mode, int LearnersEnrolled, DateTimeOffset? NextSessionAt,
    decimal RatingAverage, string PriceLabel);

public record MaterialSummary(Guid Id, string Title, string CourseTitle, string Status);

public record EmployerDashboard(
    int OpenRoles,
    int PostedThisWeek,
    int Applicants,
    int NeedReview,
    int Interviews,
    int? TimeToHireDaysMedian,
    IReadOnlyList<Candidate> Candidates,
    IReadOnlyList<UpcomingSession> UpcomingSessions);

public record Candidate(Guid ApplicationId, string Name, string Headline, string Stage, string RateLabel, string AppliedLabel);

public record JobSeekerDashboard(
    int NewMatchesThisWeek,
    int ApplicationsCount,
    int AtInterview,
    int SavedRoles,
    int? ProfileStrengthPercent,
    string? ProfileStrengthNote,
    IReadOnlyList<JobMatch> Matches,
    IReadOnlyList<MyApplication> MyApplications);

public record JobMatch(
    Guid JobPostingId, string Title, string Company, string Location, string WorkMode,
    string EmploymentType, string RateLabel, string PostedLabel);

public record MyApplication(string Title, string Company, string Stage, string AppliedLabel);
