namespace SkillBridge.ApiService.Jobs.Contracts;

public record EmployerJobListingItem(
    Guid Id,
    string Title,
    string Location,
    string WorkMode,
    string EmploymentType,
    string PostedLabel,
    int ApplicantCount);
