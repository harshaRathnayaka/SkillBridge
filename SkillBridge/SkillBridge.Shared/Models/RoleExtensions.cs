namespace SkillBridge.Shared.Models;

// The prototype's own labels (Tutor/Employer/Job seeker) don't match the backend's role names
// (Teacher/JobGiver/JobSeeker) — RoleSeeder.RoleNames and the JWT "role" claim must stay as
// they are (already-registered accounts depend on the exact string), so this is purely a
// display-layer translation, used everywhere a role is shown to a user.
public static class RoleExtensions
{
    public static string ToFriendlyName(this Role role) => role switch
    {
        Role.Teacher => "Tutor",
        Role.Student => "Student",
        Role.JobSeeker => "Job seeker",
        Role.JobGiver => "Employer",
        _ => role.ToString(),
    };

    public static string ToFriendlyName(this string role) =>
        Enum.TryParse<Role>(role, out var parsed) ? parsed.ToFriendlyName() : role;
}
