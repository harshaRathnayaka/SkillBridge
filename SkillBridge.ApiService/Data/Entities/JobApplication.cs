namespace SkillBridge.ApiService.Data;

public enum ApplicationStage
{
    New,
    Screening,
    Interview,
    Offer,
}

// ApplicantName/Headline/RateLabel are denormalized (not joined to ApplicationUser): on an
// employer's own dashboard the "candidates" are seeded demo profiles, not real accounts, so
// there's nothing to join to. When a real job seeker applies to a catalog posting, their own
// display name is copied in at creation time instead — one consistent row shape either way.
public class JobApplication
{
    public Guid Id { get; set; }
    public required string ApplicantId { get; set; }
    public required string ApplicantName { get; set; }
    public required string ApplicantHeadline { get; set; }
    public required string RateLabel { get; set; }
    public Guid JobPostingId { get; set; }
    public ApplicationStage Stage { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
    public DateTimeOffset? InterviewAt { get; set; }
}
