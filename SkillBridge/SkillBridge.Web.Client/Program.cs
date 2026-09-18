using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SkillBridge.Shared.Services;
using SkillBridge.Web.Client.Services;

namespace SkillBridge.Web.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Add device-specific services used by the SkillBridge.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();
        builder.Services.AddScoped<IAuthTokenStore, BrowserAuthTokenStore>();

        // Same-origin: WASM is served by SkillBridge.Web, whose own /api/auth/* proxy
        // endpoints (Auth/ApiProxyEndpoints.cs) are what actually get called.
        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client =>
        {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        });

        // Same same-origin reasoning, via the /api/dashboard proxy (ApiProxyEndpoints.MapDashboardProxyEndpoints).
        builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>(client =>
        {
            client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        });

        await builder.Build().RunAsync();
    }
}
