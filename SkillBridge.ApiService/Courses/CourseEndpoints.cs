using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Courses.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Dashboard;

namespace SkillBridge.ApiService.Courses;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses").RequireAuthorization();

        group.MapGet("/", GetCourseCatalogAsync);
        group.MapPost("/", CreateCourseAsync);
        group.MapPost("/{courseId:guid}/enroll", EnrollAsync);
        group.MapPost("/{courseId:guid}/materials", CreateMaterialAsync);
        group.MapPost("/materials/{materialId:guid}/publish", PublishMaterialAsync);
        group.MapPost("/materials/{materialId:guid}/unpublish", UnpublishMaterialAsync);

        return app;
    }

    private static readonly ErrorResponse Forbidden = new(["You don't have permission to do that."]);
    private static readonly ErrorResponse NotFound = new(["Not found."]);
    private static readonly ErrorResponse AlreadyEnrolled = new(["You're already enrolled in this course."]);

    // Same pattern DashboardEndpoints.GetDashboardAsync already uses — see that file's comment
    // on why this reads the raw "role"/"name" claims directly rather than RequireRole()/Identity.Name.
    private static (string? UserId, string? Role, string? Name) CallerInfo(HttpContext http) => (
        http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        http.User.FindFirst("role")?.Value,
        http.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value);

    private static async Task<IResult> CreateCourseAsync(CreateCourseRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, role, name) = CallerInfo(http);
        if (userId is null || role != "Teacher")
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Enum.TryParse<CourseMode>(request.Mode, out var mode))
        {
            return Results.BadRequest(new ErrorResponse([$"'{request.Mode}' is not a recognized course mode."]));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new ErrorResponse(["Title is required."]));
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            TeacherId = userId,
            TeacherName = name ?? "Tutor",
            Title = request.Title,
            Mode = mode,
            PriceAmount = request.PriceAmount,
            Currency = request.Currency,
            PriceUnitLabel = request.PriceUnitLabel,
            RatingAverage = 0,
            NextSessionAt = request.NextSessionAt,
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(course.Id));
    }

    private static async Task<IResult> EnrollAsync(Guid courseId, HttpContext http, ApplicationDbContext db)
    {
        var (userId, role, _) = CallerInfo(http);
        if (userId is null || role != "Student")
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var courseExists = await db.Courses.AnyAsync(c => c.Id == courseId);
        if (!courseExists)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        var alreadyEnrolled = await db.Enrollments.AnyAsync(e => e.StudentId == userId && e.CourseId == courseId);
        if (alreadyEnrolled)
        {
            return Results.Json(AlreadyEnrolled, statusCode: StatusCodes.Status409Conflict);
        }

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = userId,
            CourseId = courseId,
            LessonsTotal = 10,
            LessonsRemaining = 10,
            ProgressPercent = 0,
            NextLessonTitle = "Lesson 1",
        };

        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(enrollment.Id));
    }

    private static async Task<IResult> CreateMaterialAsync(
        Guid courseId, CreateMaterialRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, role, _) = CallerInfo(http);
        if (userId is null || role != "Teacher")
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        if (course.TeacherId != userId)
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new ErrorResponse(["Title is required."]));
        }

        var material = new Material
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = request.Title,
            Status = MaterialStatus.Draft,
        };

        db.Materials.Add(material);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(material.Id));
    }

    private static Task<IResult> PublishMaterialAsync(Guid materialId, HttpContext http, ApplicationDbContext db) =>
        SetMaterialStatusAsync(materialId, MaterialStatus.Published, http, db);

    private static Task<IResult> UnpublishMaterialAsync(Guid materialId, HttpContext http, ApplicationDbContext db) =>
        SetMaterialStatusAsync(materialId, MaterialStatus.Draft, http, db);

    private static async Task<IResult> SetMaterialStatusAsync(Guid materialId, MaterialStatus status, HttpContext http, ApplicationDbContext db)
    {
        var (userId, role, _) = CallerInfo(http);
        if (userId is null || role != "Teacher")
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == materialId);
        if (material is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == material.CourseId);
        if (course is null || course.TeacherId != userId)
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        material.Status = status;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    // Any authenticated role can browse the catalog (a page like "Find teachers" is Student-only
    // at the UI level, but the read itself isn't sensitive) — IsEnrolled is only ever true for a
    // Student, since only Students can enroll.
    private static async Task<IResult> GetCourseCatalogAsync(HttpContext http, ApplicationDbContext db)
    {
        var (userId, role, _) = CallerInfo(http);

        var enrolledCourseIds = role == "Student"
            ? (await db.Enrollments.Where(e => e.StudentId == userId).Select(e => e.CourseId).ToListAsync()).ToHashSet()
            : new HashSet<Guid>();

        var courses = await db.Courses.OrderBy(c => c.Title).ToListAsync();

        var catalog = courses
            .Select(c => new CourseCatalogItem(
                c.Id, c.TeacherName, c.Title, DashboardFormatting.FormatMode(c.Mode),
                DashboardFormatting.FormatMoney(c.PriceAmount, c.Currency, c.PriceUnitLabel),
                c.RatingAverage, c.NextSessionAt, enrolledCourseIds.Contains(c.Id)))
            .ToList();

        return Results.Ok(catalog);
    }
}
