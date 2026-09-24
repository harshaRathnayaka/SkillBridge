namespace SkillBridge.ApiService.Data;

public enum ApplicationStage
{
    New,
    Screening,
    Interview,
    Offer,
}

// ApplicantName/Headline/RateLabel are denormalized copies taken at apply time, not a live
// join to ApplicationUser — same plain-FK convention Course/JobPosting use.
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
