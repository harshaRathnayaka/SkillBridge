using Microsoft.EntityFrameworkCore;

namespace SkillBridge.ApiService.Data.Seed;

// Gives every newly registered user a small, deterministic set of starter dashboard content —
// run once per registration from AuthEndpoints.RegisterAsync, not at app startup, so each
// user's own seeded rows are isolated and easy to reason about in tests (no shared mutable
// fixture across test runs).
//
// A handful of rows are owned by fixed "system" ids that never correspond to a real account
// (SystemTeacherAId etc.) — this mirrors the Lovable prototype itself, whose demo teachers and
// employers (Dev Anand, Northwind Labs, ...) are fictional too, not other real users.
public static class DashboardContentSeeder
{
    private const string SystemTeacherAId = "system-teacher-a";
    private const string SystemTeacherBId = "system-teacher-b";
    private const string SystemTeacherCId = "system-teacher-c";
    private const string SystemEmployerId = "system-employer";

    public static async Task SeedForNewUserAsync(ApplicationDbContext db, string userId, string role, string displayName)
    {
        await EnsureCatalogSeededAsync(db);

        switch (role)
        {
            case "Student":
                await SeedStudentAsync(db, userId);
                break;
            case "Teacher":
                SeedTeacher(db, userId, displayName);
                break;
            case "JobGiver":
                SeedEmployer(db, userId, displayName);
                break;
            case "JobSeeker":
                await SeedJobSeekerAsync(db, userId);
                break;
        }

        await db.SaveChangesAsync();
    }

    // Idempotent, mirroring RoleSeeder's own "check, then create" pattern — a small shared
    // demo catalog (3 courses, 3 job postings) that every Student/JobSeeker draws on, so their
    // dashboards can show real relational data (a genuine CourseId/JobPostingId FK) instead of
    // orphaned per-user copies.
    private static async Task EnsureCatalogSeededAsync(ApplicationDbContext db)
    {
        if (await db.Courses.AnyAsync(c => c.TeacherId == SystemTeacherAId))
        {
            return;
        }

        db.Courses.AddRange(
            new Course
            {
                Id = Guid.NewGuid(), TeacherId = SystemTeacherAId, TeacherName = "Dev Anand",
                Title = "Modern Web Development", Mode = CourseMode.Live,
                PriceAmount = 65, Currency = "USD", PriceUnitLabel = " / hr",
                RatingAverage = 4.8m, NextSessionAt = DateTimeOffset.UtcNow.AddDays(2),
            },
            new Course
            {
                Id = Guid.NewGuid(), TeacherId = SystemTeacherBId, TeacherName = "Aisha Rahman",
                Title = "SQL for Analysts, Level 2", Mode = CourseMode.SelfPaced,
                PriceAmount = 45, Currency = "USD", PriceUnitLabel = " / hr",
                RatingAverage = 4.6m, NextSessionAt = DateTimeOffset.UtcNow.AddDays(5),
            },
            new Course
            {
                Id = Guid.NewGuid(), TeacherId = SystemTeacherCId, TeacherName = "Marco Bianchi",
                Title = "IELTS Speaking Intensive", Mode = CourseMode.Live,
                PriceAmount = 30, Currency = "USD", PriceUnitLabel = " / session",
                RatingAverage = 4.9m, NextSessionAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        db.JobPostings.AddRange(
            new JobPosting
            {
                Id = Guid.NewGuid(), EmployerId = SystemEmployerId, Title = "React Course Instructor",
                CompanyDisplayName = "Northwind Labs", Location = "Remote", WorkMode = "Remote",
                EmploymentType = "Contract", RateLabel = "$45 - $70 / hr", PostedAt = DateTimeOffset.UtcNow.AddHours(-5),
            },
            new JobPosting
            {
                Id = Guid.NewGuid(), EmployerId = SystemEmployerId, Title = "Frontend Engineer, Learning Platform",
                CompanyDisplayName = "Cadence", Location = "Singapore", WorkMode = "Hybrid",
                EmploymentType = "Full-time", RateLabel = "$96k - $128k", PostedAt = DateTimeOffset.UtcNow.AddDays(-1),
            },
            new JobPosting
            {
                Id = Guid.NewGuid(), EmployerId = SystemEmployerId, Title = "IELTS Speaking Coach",
                CompanyDisplayName = "Lingua Room", Location = "Remote", WorkMode = "Remote",
                EmploymentType = "Gig", RateLabel = "$28 / session", PostedAt = DateTimeOffset.UtcNow.AddDays(-3),
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedStudentAsync(ApplicationDbContext db, string studentId)
    {
        var catalogCourses = await db.Courses
            .Where(c => c.TeacherId == SystemTeacherAId || c.TeacherId == SystemTeacherBId || c.TeacherId == SystemTeacherCId)
            .OrderBy(c => c.Title)
            .Take(2)
            .ToListAsync();

        var lessonTitles = new[] { "Suspense boundaries in practice", "Window functions" };
        for (var i = 0; i < catalogCourses.Count; i++)
        {
            db.Enrollments.Add(new Enrollment
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseId = catalogCourses[i].Id,
                LessonsTotal = 16,
                LessonsRemaining = 5 + i * 6,
                ProgressPercent = 68 - i * 34,
                NextLessonTitle = lessonTitles[i % lessonTitles.Length],
            });
        }

        db.UserProfiles.Add(new UserProfile { UserId = studentId, HoursThisMonth = 18 });
    }

    private static void SeedTeacher(ApplicationDbContext db, string teacherId, string teacherDisplayName)
    {
        var courseA = new Course
        {
            Id = Guid.NewGuid(), TeacherId = teacherId, TeacherName = teacherDisplayName,
            Title = "A/L Combined Maths — Batch 04", Mode = CourseMode.Live,
            PriceAmount = 126_000, Currency = "LKR", PriceUnitLabel = null,
            RatingAverage = 4.9m, NextSessionAt = DateTimeOffset.UtcNow.AddDays(3),
        };
        var courseB = new Course
        {
            Id = Guid.NewGuid(), TeacherId = teacherId, TeacherName = teacherDisplayName,
            Title = "Intro to Data Visualisation", Mode = CourseMode.SelfPaced,
            PriceAmount = 1_840, Currency = "USD", PriceUnitLabel = null,
            RatingAverage = 4.7m, NextSessionAt = null,
        };
        db.Courses.AddRange(courseA, courseB);

        // Phantom learners padding the "enrolled" count on the tutor's own new courses — no
        // real student account exists for these ids, so they never surface on anyone's own
        // "active courses" dashboard; they only count.
        for (var i = 0; i < 9; i++)
        {
            db.Enrollments.Add(new Enrollment
            {
                Id = Guid.NewGuid(), StudentId = $"demo-learner-{teacherId}-{i}", CourseId = courseA.Id,
                LessonsTotal = 12, LessonsRemaining = 4, ProgressPercent = 60, NextLessonTitle = "Mock paper review",
            });
        }
        for (var i = 0; i < 24; i++)
        {
            db.Enrollments.Add(new Enrollment
            {
                Id = Guid.NewGuid(), StudentId = $"demo-learner-{teacherId}-b-{i}", CourseId = courseB.Id,
                LessonsTotal = 10, LessonsRemaining = 3, ProgressPercent = 70, NextLessonTitle = "Chart types worksheet",
            });
        }

        db.Materials.AddRange(
            new Material { Id = Guid.NewGuid(), CourseId = courseA.Id, Title = "Mock paper 06 + marking scheme", Status = MaterialStatus.Published },
            new Material { Id = Guid.NewGuid(), CourseId = courseB.Id, Title = "Window functions worksheet", Status = MaterialStatus.Draft });

        db.UserProfiles.Add(new UserProfile
        {
            UserId = teacherId, RatingAverage = 4.9m, RatingCount = 82,
            EarningsLabel = "LKR 174,000", EarningsTrendPercent = 12m,
        });
    }

    private static void SeedEmployer(ApplicationDbContext db, string employerId, string employerDisplayName)
    {
        var posting = new JobPosting
        {
            Id = Guid.NewGuid(), EmployerId = employerId, Title = "Senior Maths Tutor",
            CompanyDisplayName = employerDisplayName, Location = "Colombo, LK", WorkMode = "Hybrid",
            EmploymentType = "Part-time", RateLabel = "LKR 3,800 / hr", PostedAt = DateTimeOffset.UtcNow.AddDays(-2),
        };
        db.JobPostings.Add(posting);

        db.JobApplications.AddRange(
            new JobApplication
            {
                Id = Guid.NewGuid(), ApplicantId = $"demo-applicant-{employerId}-1", ApplicantName = "Nimali Fernando",
                ApplicantHeadline = "A/L Mathematics tutor · 7 yrs", RateLabel = "LKR 3,800 / hr",
                JobPostingId = posting.Id, Stage = ApplicationStage.Interview, AppliedAt = DateTimeOffset.UtcNow.AddHours(-20),
                InterviewAt = DateTimeOffset.UtcNow.AddDays(1),
            },
            new JobApplication
            {
                Id = Guid.NewGuid(), ApplicantId = $"demo-applicant-{employerId}-2", ApplicantName = "Dev Anand",
                ApplicantHeadline = "Staff engineer, ex-Shopify", RateLabel = "$65 / hr",
                JobPostingId = posting.Id, Stage = ApplicationStage.Screening, AppliedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });

        db.UserProfiles.Add(new UserProfile { UserId = employerId, TimeToHireDaysMedian = 14 });
    }

    private static async Task SeedJobSeekerAsync(ApplicationDbContext db, string jobSeekerId)
    {
        var catalogPosting = await db.JobPostings
            .Where(j => j.EmployerId == SystemEmployerId)
            .OrderBy(j => j.Title)
            .FirstAsync();

        db.JobApplications.Add(new JobApplication
        {
            Id = Guid.NewGuid(), ApplicantId = jobSeekerId, ApplicantName = "You",
            ApplicantHeadline = "", RateLabel = "", JobPostingId = catalogPosting.Id,
            Stage = ApplicationStage.Interview, AppliedAt = DateTimeOffset.UtcNow,
        });

        db.UserProfiles.Add(new UserProfile
        {
            UserId = jobSeekerId, ProfileStrengthPercent = 82, ProfileStrengthNote = "add 2 references", SavedRoles = 5,
        });
    }
}
