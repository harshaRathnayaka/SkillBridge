namespace SkillBridge.ApiService.Data;

// EmployerId is a real employer's own id for postings they create, or a small fixed
// "system employer" id for the shared demo catalog job seekers see roles from (see
// DashboardContentSeeder) — the same non-account-owner pattern Course uses for teachers.
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
