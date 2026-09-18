using Microsoft.AspNetCore.Components.Authorization;
using SkillBridge.Web.Auth;
using SkillBridge.Web.Components;
using SkillBridge.Shared.Services;
using SkillBridge.Web.Services;

namespace SkillBridge;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // Add device-specific services used by the SkillBridge.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();
        builder.Services.AddScoped<IAuthTokenStore, BrowserAuthTokenStore>();

        // Talks to the real SkillBridge.ApiService (Aspire service discovery resolves the
        // "https+http://skillbridge-apiservice" scheme) — used only by this host's own
        // /api/auth/* passthrough endpoints (Auth/ApiProxyEndpoints.cs), which exist so
        // Web.Client (WASM) can reach the ApiService same-origin without CORS.
        builder.Services.AddHttpClient("ApiService", client =>
        {
            client.BaseAddress = new Uri("https+http://skillbridge-apiservice");
        });

        // Used by Login.razor/Register.razor/NavMenu.razor (shared UI) — points back at this
        // same host's own /api/auth/* endpoints rather than directly at the ApiService.
        // BaseAddress is left unset here and filled in lazily by AuthApiClient itself from the
        // injected NavigationManager — see the comment on AuthApiClient for why it can't be
        // set here.
        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>();
        builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapAuthProxyEndpoints();
        app.MapDashboardProxyEndpoints();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(SkillBridge.Shared._Imports).Assembly,
                typeof(SkillBridge.Web.Client._Imports).Assembly);

        app.Run();
    }
}
