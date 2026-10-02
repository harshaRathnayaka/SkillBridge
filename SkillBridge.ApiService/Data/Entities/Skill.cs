namespace SkillBridge.ApiService.Data;

// A tag-style skill shared by every user and role, so both marketplaces can eventually match on
// it. NormalizedName (upper-cased, whitespace-collapsed) is unique; Name keeps the casing of
// whoever added it first.
public class Skill
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
}
