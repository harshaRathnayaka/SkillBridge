using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Auth;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Profiles.Contracts;

namespace SkillBridge.ApiService.Profiles;

public static partial class ProfileEndpoints
{
    private const int MaxHeadlineLength = 120;
    private const int MaxBioLength = 1000;
    private const int MaxSkillNameLength = 40;
    private const int MaxSkillsPerUser = 20;
    private const int MaxReferencesPerUser = 10;
    private const int MaxReferenceNameLength = 100;
    private const int MaxReferenceRelationshipLength = 100;
    private const int MaxReferenceContactLength = 200;
    private const int MaxReferenceCommentLength = 500;

    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").RequireAuthorization();

        group.MapGet("/", GetProfileAsync);
        group.MapPut("/", UpdateProfileAsync);
        group.MapPost("/skills", AddSkillAsync);
        group.MapDelete("/skills/{skillId:guid}", RemoveSkillAsync);
        group.MapPost("/references", AddReferenceAsync);
        group.MapDelete("/references/{referenceId:guid}", RemoveReferenceAsync);

        return app;
    }

    private static readonly ErrorResponse Forbidden = new(["You don't have permission to do that."]);
    private static readonly ErrorResponse NotFound = new(["Not found."]);
    private static readonly ErrorResponse AlreadyHasSkill = new(["You already have this skill."]);

    // The route group requires authorization, and every issued token carries a "sub" claim.
    private static (string UserId, string[] Roles) Caller(HttpContext http)
    {
        var (userId, roles, _) = CallerContext.From(http);
        return (userId!, roles);
    }

    private static bool CanHaveReferences(string[] roles) => roles.Contains("Teacher") || roles.Contains("JobSeeker");

    private static async Task<IResult> GetProfileAsync(HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles) = Caller(http);

        var profile = await db.UserProfiles.FindAsync(userId);

        var skills = (await (
                from userSkill in db.UserSkills
                where userSkill.UserId == userId
                join skill in db.Skills on userSkill.SkillId equals skill.Id
                select new { skill.Id, skill.Name }).ToListAsync())
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new ProfileSkillItem(s.Id, s.Name))
            .ToList();

        // Ordered client-side — SQLite's EF Core provider can't ORDER BY a DateTimeOffset.
        var references = (await db.ProfileReferences.Where(r => r.UserId == userId).ToListAsync())
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ProfileReferenceItem(r.Id, r.Name, r.Relationship, r.Contact, r.Comment))
            .ToList();

        var (percent, note) = ProfileStrength.Compute(
            profile?.Headline, profile?.Bio, skills.Count, references.Count, CanHaveReferences(roles));

        return Results.Ok(new ProfileResponse(
            profile?.Headline ?? "", profile?.Bio ?? "", skills, references, percent, note));
    }

    private static async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, _) = Caller(http);

        var headline = request.Headline?.Trim() ?? "";
        var bio = request.Bio?.Trim() ?? "";
        if (headline.Length > MaxHeadlineLength)
        {
            return Results.BadRequest(new ErrorResponse([$"Headline must be {MaxHeadlineLength} characters or fewer."]));
        }

        if (bio.Length > MaxBioLength)
        {
            return Results.BadRequest(new ErrorResponse([$"Bio must be {MaxBioLength} characters or fewer."]));
        }

        var profile = await db.UserProfiles.FindAsync(userId);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId };
            db.UserProfiles.Add(profile);
        }

        profile.Headline = headline;
        profile.Bio = bio;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> AddSkillAsync(AddSkillRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, _) = Caller(http);

        var name = WhitespaceRun().Replace(request.Name?.Trim() ?? "", " ");
        if (name.Length == 0 || name.Length > MaxSkillNameLength)
        {
            return Results.BadRequest(new ErrorResponse([$"Skill must be 1 to {MaxSkillNameLength} characters."]));
        }

        var normalized = name.ToUpperInvariant();
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.NormalizedName == normalized);

        if (skill is not null && await db.UserSkills.AnyAsync(us => us.UserId == userId && us.SkillId == skill.Id))
        {
            return Results.Json(AlreadyHasSkill, statusCode: StatusCodes.Status409Conflict);
        }

        if (await db.UserSkills.CountAsync(us => us.UserId == userId) >= MaxSkillsPerUser)
        {
            return Results.BadRequest(new ErrorResponse([$"You can add up to {MaxSkillsPerUser} skills."]));
        }

        if (skill is null)
        {
            skill = new Skill { Id = Guid.NewGuid(), Name = name, NormalizedName = normalized };
            db.Skills.Add(skill);
        }

        db.UserSkills.Add(new UserSkill { Id = Guid.NewGuid(), UserId = userId, SkillId = skill.Id });
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(skill.Id));
    }

    private static async Task<IResult> RemoveSkillAsync(Guid skillId, HttpContext http, ApplicationDbContext db)
    {
        var (userId, _) = Caller(http);

        var link = await db.UserSkills.FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skillId);
        if (link is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        db.UserSkills.Remove(link);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> AddReferenceAsync(AddReferenceRequest request, HttpContext http, ApplicationDbContext db)
    {
        var (userId, roles) = Caller(http);
        if (!CanHaveReferences(roles))
        {
            return Results.Json(Forbidden, statusCode: StatusCodes.Status403Forbidden);
        }

        var name = request.Name?.Trim() ?? "";
        var relationship = request.Relationship?.Trim() ?? "";
        var contact = NullIfBlank(request.Contact);
        var comment = NullIfBlank(request.Comment);

        if (name.Length == 0 || relationship.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse(["Name and relationship are required."]));
        }

        if (name.Length > MaxReferenceNameLength
            || relationship.Length > MaxReferenceRelationshipLength
            || contact?.Length > MaxReferenceContactLength
            || comment?.Length > MaxReferenceCommentLength)
        {
            return Results.BadRequest(new ErrorResponse(["One of the reference fields is too long."]));
        }

        if (await db.ProfileReferences.CountAsync(r => r.UserId == userId) >= MaxReferencesPerUser)
        {
            return Results.BadRequest(new ErrorResponse([$"You can add up to {MaxReferencesPerUser} references."]));
        }

        var reference = new ProfileReference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Relationship = relationship,
            Contact = contact,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.ProfileReferences.Add(reference);
        await db.SaveChangesAsync();

        return Results.Ok(new CreatedResponse(reference.Id));
    }

    private static async Task<IResult> RemoveReferenceAsync(Guid referenceId, HttpContext http, ApplicationDbContext db)
    {
        var (userId, _) = Caller(http);

        var reference = await db.ProfileReferences.FirstOrDefaultAsync(r => r.Id == referenceId && r.UserId == userId);
        if (reference is null)
        {
            return Results.Json(NotFound, statusCode: StatusCodes.Status404NotFound);
        }

        db.ProfileReferences.Remove(reference);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
