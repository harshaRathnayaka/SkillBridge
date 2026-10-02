using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Auth;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Dashboard;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Jobs.Contracts;

namespace SkillBridge.ApiService.Jobs;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").RequireAuthorization();

        group.MapGet("/", GetJobCatalogAsync);
        group.MapGet("/mine", GetMyJobPostingsAsync);
        group.MapPost("/", CreateJobPostingAsync);
        group.MapPost("/{jobPostingId:guid}/apply", ApplyAsync);
        group.MapPost("/applications/{applicationId:guid}/advance", AdvanceStageAsync);

        return app;
    }

    private static readonly ErrorResponse Forbidden = new(["You don't have permission to do that."]);
    private static readonly ErrorResponse NotFound = new(["Not found."]);
    private static readonly ErrorResponse AlreadyApplied = new(["You've already applied to this role."]);

    private static async Task<IResult> CreateJobPostingAsync(CreateJobPostingRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles, name) = CallerContext.From(http);
        if (userId is null || !roles.Contains("JobGiver"))
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new ErrorResponse(["Title is required."]));
        }

        var posting = new JobPosting
        {
            Id = Guid.NewGuid(),
            EmployerId = userId,
            Title = request.Title,
            CompanyDisplayName = name ?? "Employer",
            Location = request.Location,
            WorkMode = request.WorkMode,
            EmploymentType = request.EmploymentType,
            RateLabel = request.RateLabel,
            PostedAt = DateTimeOffset.UtcNow,
        };

        db.JobPostings.Add(posting);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(posting.Id));
    }

    private static async Task<IResult> ApplyAsync(Guid jobPostingId, HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles, name) = CallerContext.From(http);
        if (userId is null || !roles.Contains("JobSeeker"))
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var posting = await db.JobPostings.FirstOrDefaultAsync(j => j.Id == jobPostingId);
        if (posting is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        var alreadyApplied = await db.JobApplications.AnyAsync(a => a.ApplicantId == userId && a.JobPostingId == jobPostingId);
        if (alreadyApplied)
        {
            return Results.Json(AlreadyApplied, statusCode: StatusCodes.Status409Conflict);
        }

        // Snapshotted at apply time (like ApplicantName), so the employer's pipeline keeps showing
        // what the candidate's headline was when they applied.
        var applicantProfile = await db.UserProfiles.FindAsync(userId);

        var application = new JobApplication
        {
            Id = Guid.NewGuid(),
            ApplicantId = userId,
            ApplicantName = name ?? "Job seeker",
            ApplicantHeadline = applicantProfile?.Headline ?? "",
            RateLabel = posting.RateLabel,
            JobPostingId = jobPostingId,
            Stage = ApplicationStage.New,
            AppliedAt = DateTimeOffset.UtcNow,
        };

        db.JobApplications.Add(application);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(application.Id));
    }

    private static async Task<IResult> AdvanceStageAsync(Guid applicationId, HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles, _) = CallerContext.From(http);
        if (userId is null || !roles.Contains("JobGiver"))
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var application = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        var posting = await db.JobPostings.FirstOrDefaultAsync(j => j.Id == application.JobPostingId);
        if (posting is null || posting.EmployerId != userId)
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        application.Stage = application.Stage switch
        {
            ApplicationStage.New => ApplicationStage.Screening,
            ApplicationStage.Screening => ApplicationStage.Interview,
            ApplicationStage.Interview => ApplicationStage.Offer,
            _ => application.Stage,
        };

        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    // Any authenticated role can browse the catalog (a page like "Find work" is JobSeeker-only
    // at the UI level, but the read itself isn't sensitive) — IsApplied is only ever true for a
    // JobSeeker, since only JobSeekers can apply.
    private static async Task<IResult> GetJobCatalogAsync(HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles, _) = CallerContext.From(http);

        var postings = (await db.JobPostings.ToListAsync()).OrderByDescending(p => p.PostedAt).ToList();
        var applicantCounts = ApplicantCountsFor(await db.JobApplications.ToListAsync());

        var appliedPostingIds = roles.Contains("JobSeeker")
            ? (await db.JobApplications.Where(a => a.ApplicantId == userId).Select(a => a.JobPostingId).ToListAsync()).ToHashSet()
            : new HashSet<Guid>();

        var catalog = postings
            .Select(p => new JobCatalogItem(
                p.Id, p.Title, p.CompanyDisplayName, p.Location, p.WorkMode, p.EmploymentType, p.RateLabel,
                DashboardFormatting.FormatRelative(p.PostedAt), applicantCounts.GetValueOrDefault(p.Id), appliedPostingIds.Contains(p.Id)))
            .ToList();

        return Results.Ok(catalog);
    }

    private static async Task<IResult> GetMyJobPostingsAsync(HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles, _) = CallerContext.From(http);
        if (userId is null || !roles.Contains("JobGiver"))
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var postings = (await db.JobPostings.Where(p => p.EmployerId == userId).ToListAsync())
            .OrderByDescending(p => p.PostedAt)
            .ToList();
        var postingIds = postings.Select(p => p.Id).ToHashSet();
        var applicantCounts = ApplicantCountsFor(
            await db.JobApplications.Where(a => postingIds.Contains(a.JobPostingId)).ToListAsync());

        var listings = postings
            .Select(p => new EmployerJobListingItem(
                p.Id, p.Title, p.Location, p.WorkMode, p.EmploymentType,
                DashboardFormatting.FormatRelative(p.PostedAt), applicantCounts.GetValueOrDefault(p.Id)))
            .ToList();

        return Results.Ok(listings);
    }

    // Grouped in-memory rather than via a GroupBy translated to SQL — kept consistent with this
    // codebase's established SQLite-safety pattern (see DashboardEndpoints' comments) of
    // fetching first and aggregating client-side rather than risking a query shape the SQLite
    // EF Core provider can't translate.
    private static Dictionary<Guid, int> ApplicantCountsFor(List<JobApplication> applications) =>
        applications
            .GroupBy(a => a.JobPostingId)
            .ToDictionary(g => g.Key, g => g.Count());
}
