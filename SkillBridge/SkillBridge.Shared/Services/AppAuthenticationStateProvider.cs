using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SkillBridge.Shared.Services;

// Shared across all 3 hosts — builds the UI's ClaimsPrincipal from the access token's own
// claims. Each host supplies its own IAuthTokenStore (MAUI: SecureStorage; Web/Web.Client:
// BrowserAuthTokenStore over sessionStorage). This never re-validates the JWT signature:
// that's the API's job on every request; here it's purely for driving
// <AuthorizeView>/<AuthorizeRouteView> UI state.
public class AppAuthenticationStateProvider(IAuthTokenStore tokenStore) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        StoredTokens? tokens;
        try
        {
            tokens = await tokenStore.GetTokensAsync();
        }
        catch (InvalidOperationException)
        {
            // JS interop (used by the Web hosts' BrowserAuthTokenStore) isn't available yet
            // during Blazor Server's static prerender pass. The interactive circuit that
            // follows re-evaluates authentication state correctly once it connects.
            return new AuthenticationState(Anonymous);
        }

        if (tokens is null || tokens.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new AuthenticationState(Anonymous);
        }

        return new AuthenticationState(BuildPrincipal(tokens.AccessToken));
    }

    private static ClaimsPrincipal BuildPrincipal(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var claims = jwt.Claims.Select(c => c.Type == "role" ? new Claim(ClaimTypes.Role, c.Value) : c);
        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer", nameType: JwtRegisteredClaimNames.Name, roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
