namespace SkillBridge.ApiService.Data;

// EmployerId is the real employer's own id — same plain-FK convention Course.TeacherId uses,
// kept for consistency rather than a real join.
public class JobPosting
{
    public Guid Id { get; set; }
    public required string EmployerId { get; set; }
    public required string Title { get; set; }
    public required string CompanyDisplayName { get; set; }
    public required string Location { get; set; }
    public required string WorkMode { get; set; }
    public required string EmploymentType { get; set; }
    public required string RateLabel { get; set; }
    public DateTimeOffset PostedAt { get; set; }
}
