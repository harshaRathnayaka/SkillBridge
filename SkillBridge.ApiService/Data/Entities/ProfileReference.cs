namespace SkillBridge.ApiService.Data;

// Professional reference for a job seeker; doubles as an endorsement on a tutor's profile.
public class ProfileReference
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string Relationship { get; set; }
    public string? Contact { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
