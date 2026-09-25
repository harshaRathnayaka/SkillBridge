namespace SkillBridge.ApiService.Jobs.Contracts;

public record JobCatalogItem(
    Guid Id,
    string Title,
    string CompanyDisplayName,
    string Location,
    string WorkMode,
    string EmploymentType,
    string RateLabel,
    string PostedLabel,
    int ApplicantCount,
    bool IsApplied);
