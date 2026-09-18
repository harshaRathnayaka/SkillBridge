using Microsoft.AspNetCore.Identity;

namespace SkillBridge.ApiService.Data;

public class ApplicationUser : IdentityUser
{
    public required string DisplayName { get; set; }
}
