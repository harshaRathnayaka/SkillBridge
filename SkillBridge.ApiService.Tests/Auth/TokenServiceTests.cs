using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using SkillBridge.ApiService.Auth;
using SkillBridge.ApiService.Data;

namespace SkillBridge.ApiService.Tests.Auth;

public class TokenServiceTests
{
    private static ITokenService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "SkillBridge.ApiService.Tests",
                ["Jwt:Audience"] = "SkillBridge.Tests",
            })
            .Build();

        return new TokenService(config);
    }

    [Fact]
    public void CreateAccessToken_includes_one_role_claim_per_assigned_role()
    {
        var service = BuildService();
        var user = new ApplicationUser { Id = "user-1", Email = "teacher@example.com", DisplayName = "Ada" };

        var jwt = service.CreateAccessToken(user, ["Teacher"], DateTimeOffset.UtcNow.AddMinutes(15));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var roleClaims = token.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Equal(["Teacher"], roleClaims);
        Assert.Equal("user-1", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Ada", token.Claims.Single(c => c.Type == "name").Value);
    }

    [Fact]
    public void CreateAccessToken_supports_a_user_holding_multiple_roles()
    {
        var service = BuildService();
        var user = new ApplicationUser { Id = "user-2", Email = "multi@example.com", DisplayName = "Grace" };

        var jwt = service.CreateAccessToken(user, ["Teacher", "JobGiver"], DateTimeOffset.UtcNow.AddMinutes(15));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var roleClaims = token.Claims.Where(c => c.Type == "role").Select(c => c.Value).OrderBy(x => x).ToList();
        Assert.Equal(["JobGiver", "Teacher"], roleClaims);
    }
}
