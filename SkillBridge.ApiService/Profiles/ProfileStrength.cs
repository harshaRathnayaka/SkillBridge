using Microsoft.EntityFrameworkCore;
using SkillBridge.ApiService.Data;

namespace SkillBridge.ApiService.Profiles;

// Profile strength is computed, never stored. Weights: headline 25, bio 25, skills 10 each up to
// three (30), references 20. References only count for accounts that can have them (Teacher /
// JobSeeker) — for everyone else the score is scaled over the remaining 80 points, so a Student
// or Employer can still reach 100%.
public static class ProfileStrength
{
    public const int SkillsForFullScore = 3;

    private const int HeadlinePoints = 25;
    private const int BioPoints = 25;
    private const int PointsPerSkill = 10;
    private const int ReferencePoints = 20;

    public static (int Percent, string? Note) Compute(
        string? headline, string? bio, int skillCount, int referenceCount, bool referencesApply)
    {
        var hasHeadline = !string.IsNullOrWhiteSpace(headline);
        var hasBio = !string.IsNullOrWhiteSpace(bio);
        var skillsCounted = Math.Min(skillCount, SkillsForFullScore);
        var hasReference = referencesApply && referenceCount > 0;

        var earned = (hasHeadline ? HeadlinePoints : 0)
            + (hasBio ? BioPoints : 0)
            + skillsCounted * PointsPerSkill
            + (hasReference ? ReferencePoints : 0);
        var possible = HeadlinePoints + BioPoints + SkillsForFullScore * PointsPerSkill
            + (referencesApply ? ReferencePoints : 0);

        var percent = (int)Math.Round(earned * 100.0 / possible, MidpointRounding.AwayFromZero);

        var skillsMissing = SkillsForFullScore - skillsCounted;
        var note = !hasHeadline ? "Add a headline"
            : !hasBio ? "Add a short bio"
            : skillsMissing > 0 ? $"Add {skillsMissing} more skill{(skillsMissing == 1 ? "" : "s")}"
            : referencesApply && !hasReference ? "Add a reference"
            : null;

        return (percent, note);
    }

    public static async Task<(int Percent, string? Note)> ForUserAsync(
        ApplicationDbContext db, string userId, bool referencesApply)
    {
        var profile = await db.UserProfiles.FindAsync(userId);
        var skillCount = await db.UserSkills.CountAsync(us => us.UserId == userId);
        var referenceCount = await db.ProfileReferences.CountAsync(r => r.UserId == userId);

        return Compute(profile?.Headline, profile?.Bio, skillCount, referenceCount, referencesApply);
    }
}
