using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Dashboard.Contracts;

namespace SkillBridge.ApiService.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", GetDashboardAsync).RequireAuthorization();
        return app;
    }

    private static readonly ErrorResponse Unauthenticated = new(["Not authenticated."]);

    private static async Task<IResult> GetDashboardAsync(HttpContext http, ApplicationDbContext db)
    {
        var userId = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Json(Unauthenticated, statusCode: StatusCodes.Status401Unauthorized);
        }

        // "role" claims are mapped to ClaimTypes.Role client-side (AppAuthenticationStateProvider)
        // but arrive here exactly as TokenService issued them — see AuthEndpoints.ChangePasswordAsync
        // for the same Sub-claim pattern this reuses.
        var role = http.User.FindFirst("role")?.Value;

        var response = role switch
        {
            "Student" => new DashboardResponse("Student", await BuildStudentDashboardAsync(db, userId), null, null, null),
            "Teacher" => new DashboardResponse("Teacher", null, await BuildTutorDashboardAsync(db, userId), null, null),
            "JobGiver" => new DashboardResponse("JobGiver", null, null, await BuildEmployerDashboardAsync(db, userId), null),
            "JobSeeker" => new DashboardResponse("JobSeeker", null, null, null, await BuildJobSeekerDashboardAsync(db, userId)),
            _ => new DashboardResponse(role ?? "Unknown", null, null, null, null),
        };

        return Results.Ok(response);
    }

    private static async Task<StudentDashboard> BuildStudentDashboardAsync(ApplicationDbContext db, string studentId)
    {
        var enrollments = await db.Enrollments.Where(e => e.StudentId == studentId).ToListAsync();
        var courseIds = enrollments.Select(e => e.CourseId).ToList();
        var courses = await db.Courses.Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var profile = await db.UserProfiles.FindAsync(studentId);

        var studentCourses = enrollments
            .Where(e => courses.ContainsKey(e.CourseId))
            .Select(e =>
            {
                var course = courses[e.CourseId];
                return new StudentCourse(
                    course.Title, course.TeacherName, DashboardFormatting.FormatMode(course.Mode),
                    e.LessonsRemaining, e.ProgressPercent, e.NextLessonTitle, course.NextSessionAt);
            })
            .ToList();

        var upcomingSessions = courses.Values
            .Where(c => c.NextSessionAt is not null)
            .OrderBy(c => c.NextSessionAt)
            .Select(c => new UpcomingSession(c.Title, c.TeacherName, c.NextSessionAt!.Value))
            .ToList();

        // "Suggested teachers" — any courses the student isn't already enrolled in, capped at
        // 3. Deliberately not excluding teachers the student already has (the prototype itself
        // suggests teachers you already use), just courses already taken.
        var suggested = await db.Courses
            .Where(c => !courseIds.Contains(c.Id))
            .OrderBy(c => c.Title)
            .Take(3)
            .Select(c => new SuggestedTeacher(c.TeacherName, c.Title, DashboardFormatting.FormatMoney(c.PriceAmount, c.Currency, c.PriceUnitLabel)))
            .ToListAsync();

        return new StudentDashboard(
            ActiveCourses: enrollments.Count,
            LessonsRemainingTotal: enrollments.Sum(e => e.LessonsRemaining),
            DistinctTeachers: courses.Values.Select(c => c.TeacherId).Distinct().Count(),
            HoursThisMonth: profile?.HoursThisMonth,
            AvgProgressPercent: enrollments.Count > 0 ? (int)Math.Round(enrollments.Average(e => e.ProgressPercent)) : 0,
            Courses: studentCourses,
            UpcomingSessions: upcomingSessions,
            SuggestedTeachers: suggested);
    }

    private static async Task<TutorDashboard> BuildTutorDashboardAsync(ApplicationDbContext db, string teacherId)
    {
        var courses = await db.Courses.Where(c => c.TeacherId == teacherId).ToListAsync();
        var courseIds = courses.Select(c => c.Id).ToList();
        var enrollmentCounts = await db.Enrollments
            .Where(e => courseIds.Contains(e.CourseId))
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToListAsync();
        var materials = await db.Materials.Where(m => courseIds.Contains(m.CourseId)).ToListAsync();
        var profile = await db.UserProfiles.FindAsync(teacherId);

        int LearnersFor(Guid courseId) => enrollmentCounts.FirstOrDefault(x => x.CourseId == courseId)?.Count ?? 0;

        var tutorCourses = courses
            .Select(c => new TutorCourse(
                c.Title, DashboardFormatting.FormatMode(c.Mode), LearnersFor(c.Id), c.NextSessionAt, c.RatingAverage,
                DashboardFormatting.FormatMoney(c.PriceAmount, c.Currency, c.PriceUnitLabel)))
            .ToList();

        var upcomingSessions = courses
            .Where(c => c.NextSessionAt is not null)
            .OrderBy(c => c.NextSessionAt)
            .Select(c => new UpcomingSession(c.Title, $"{LearnersFor(c.Id)} learners", c.NextSessionAt!.Value))
            .ToList();

        var recentMaterials = materials
            .Select(m => new MaterialSummary(m.Title, courses.First(c => c.Id == m.CourseId).Title, m.Status.ToString()))
            .ToList();

        return new TutorDashboard(
            LiveClasses: courses.Count(c => c.Mode == CourseMode.Live),
            LearnersEnrolledTotal: enrollmentCounts.Sum(x => x.Count),
            PublishedMaterials: materials.Count(m => m.Status == MaterialStatus.Published),
            DraftMaterials: materials.Count(m => m.Status == MaterialStatus.Draft),
            EarningsLabel: profile?.EarningsLabel ?? "—",
            EarningsTrendPercent: profile?.EarningsTrendPercent,
            RatingAverage: profile?.RatingAverage,
            RatingCount: profile?.RatingCount,
            Courses: tutorCourses,
            UpcomingSessions: upcomingSessions,
            RecentMaterials: recentMaterials);
    }

    private static async Task<EmployerDashboard> BuildEmployerDashboardAsync(ApplicationDbContext db, string employerId)
    {
        var postings = await db.JobPostings.Where(j => j.EmployerId == employerId).ToListAsync();
        var postingIds = postings.Select(p => p.Id).ToList();
        // Ordered client-side after fetching: SQLite's EF Core provider can't translate
        // ORDER BY over a DateTimeOffset column server-side.
        var applications = (await db.JobApplications
            .Where(a => postingIds.Contains(a.JobPostingId))
            .ToListAsync())
            .OrderByDescending(a => a.AppliedAt)
            .ToList();
        var profile = await db.UserProfiles.FindAsync(employerId);

        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var candidates = applications
            .Select(a => new Candidate(a.ApplicantName, a.ApplicantHeadline, a.Stage.ToString(), a.RateLabel, DashboardFormatting.FormatRelative(a.AppliedAt)))
            .ToList();

        var upcomingSessions = applications
            .Where(a => a.InterviewAt is not null)
            .OrderBy(a => a.InterviewAt)
            .Select(a => new UpcomingSession($"Interview · {a.ApplicantName}", a.ApplicantHeadline, a.InterviewAt!.Value))
            .ToList();

        return new EmployerDashboard(
            OpenRoles: postings.Count,
            PostedThisWeek: postings.Count(p => p.PostedAt >= weekAgo),
            Applicants: applications.Count,
            NeedReview: applications.Count(a => a.Stage is ApplicationStage.New or ApplicationStage.Screening),
            Interviews: applications.Count(a => a.Stage == ApplicationStage.Interview),
            TimeToHireDaysMedian: profile?.TimeToHireDaysMedian,
            Candidates: candidates,
            UpcomingSessions: upcomingSessions);
    }

    private static async Task<JobSeekerDashboard> BuildJobSeekerDashboardAsync(ApplicationDbContext db, string jobSeekerId)
    {
        // Both queries below are ordered client-side after fetching: SQLite's EF Core provider
        // can't translate ORDER BY over a DateTimeOffset column server-side.
        var myApplications = (await db.JobApplications
            .Where(a => a.ApplicantId == jobSeekerId)
            .ToListAsync())
            .OrderByDescending(a => a.AppliedAt)
            .ToList();
        var appliedPostingIds = myApplications.Select(a => a.JobPostingId).ToHashSet();
        var profile = await db.UserProfiles.FindAsync(jobSeekerId);
        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);

        // Fetched once and filtered/ordered/counted entirely client-side: combining a
        // HashSet.Contains exclusion with a DateTimeOffset comparison in one query isn't
        // translatable by SQLite's EF Core provider.
        var allPostings = await db.JobPostings.ToListAsync();
        var unappliedPostings = allPostings.Where(j => !appliedPostingIds.Contains(j.Id)).ToList();

        var matches = unappliedPostings
            .OrderByDescending(j => j.PostedAt)
            .Take(4)
            .Select(j => new JobMatch(j.Title, j.CompanyDisplayName, j.Location, j.WorkMode, j.EmploymentType, j.RateLabel, DashboardFormatting.FormatRelative(j.PostedAt)))
            .ToList();

        var myApplicationPostings = allPostings
            .Where(j => appliedPostingIds.Contains(j.Id))
            .ToDictionary(j => j.Id);

        var myApplicationSummaries = myApplications
            .Where(a => myApplicationPostings.ContainsKey(a.JobPostingId))
            .Select(a =>
            {
                var posting = myApplicationPostings[a.JobPostingId];
                return new MyApplication(posting.Title, posting.CompanyDisplayName, a.Stage.ToString(), DashboardFormatting.FormatRelative(a.AppliedAt));
            })
            .ToList();

        var newMatchesThisWeek = unappliedPostings.Count(j => j.PostedAt >= weekAgo);

        return new JobSeekerDashboard(
            NewMatchesThisWeek: newMatchesThisWeek,
            ApplicationsCount: myApplications.Count,
            AtInterview: myApplications.Count(a => a.Stage == ApplicationStage.Interview),
            SavedRoles: profile?.SavedRoles ?? 0,
            ProfileStrengthPercent: profile?.ProfileStrengthPercent,
            ProfileStrengthNote: profile?.ProfileStrengthNote,
            Matches: matches,
            MyApplications: myApplicationSummaries);
    }
}
