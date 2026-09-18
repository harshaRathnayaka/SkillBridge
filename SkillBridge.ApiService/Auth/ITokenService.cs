using SkillBridge.ApiService.Data;

namespace SkillBridge.ApiService.Auth;

public interface ITokenService
{
    string CreateAccessToken(ApplicationUser user, IEnumerable<string> roles, DateTimeOffset expiresAtUtc);
}
