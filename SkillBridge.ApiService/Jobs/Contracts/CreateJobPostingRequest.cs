namespace SkillBridge.ApiService.Jobs.Contracts;

public record CreateJobPostingRequest(
    string Title,
    string Location,
    string WorkMode,
    string EmploymentType,
    string RateLabel);
