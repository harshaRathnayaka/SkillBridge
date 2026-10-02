namespace SkillBridge.ApiService.Data;

public class UserSkill
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public Guid SkillId { get; set; }
}
