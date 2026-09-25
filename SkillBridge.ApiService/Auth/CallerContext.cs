using System.IdentityModel.Tokens.Jwt;

namespace SkillBridge.ApiService.Auth;

// Shared by CourseEndpoints/JobEndpoints/DashboardEndpoints (previously three separate
// near-identical copies, two of them named CallerInfo). Reads every "role" claim rather than
// just the first — the caller's account may hold more than one role — and reads the raw "role"
// claim type directly rather than RequireRole()/Identity.Name, since TokenService issues a
// short "role" claim with MapInboundClaims = false and no RoleClaimType override, so the
// built-in role-checking helpers wouldn't match server-side (role claims are only remapped to
// ClaimTypes.Role client-side, in AppAuthenticationStateProvider).
public static class CallerContext
{
    public static (string? UserId, string[] Roles, string? Name) From(HttpContext http) => (
        http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        http.User.FindAll("role").Select(c => c.Value).ToArray(),
        http.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value);
}
