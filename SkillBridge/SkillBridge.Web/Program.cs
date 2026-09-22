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

        // Talks to the real SkillBridge.ApiService — used only by this host's own /api/auth/*
        // passthrough endpoints (Auth/ApiProxyEndpoints.cs), which exist so Web.Client (WASM)
        // can reach the ApiService same-origin without CORS. Locally (via the Aspire AppHost),
        // Aspire's service discovery resolves the "https+http://skillbridge-apiservice" scheme
        // automatically; outside Aspire (e.g. deployed to Fly.io as a standalone container),
        // set ApiService:BaseUrl explicitly to the ApiService's real reachable URL.
        var apiServiceBaseUrl = builder.Configuration["ApiService:BaseUrl"] ?? "https+http://skillbridge-apiservice";
        builder.Services.AddHttpClient("ApiService", client =>
        {
            client.BaseAddress = new Uri(apiServiceBaseUrl);
        });

        // Used by Login.razor/Register.razor/NavMenu.razor (shared UI) — points back at this
        // same host's own /api/auth/* endpoints rather than directly at the ApiService.
        // BaseAddress is left unset here and filled in lazily by AuthApiClient itself from the
        // injected NavigationManager — see the comment on AuthApiClient for why it can't be
        // set here.
        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>();
        builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>();
        builder.Services.AddHttpClient<IMarketplaceApiClient, MarketplaceApiClient>();

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

        // Skipped in production: Fly.io (and most PaaS hosts) terminate TLS at the edge and
        // forward plain HTTP internally, so redirecting-to-HTTPS inside the container would
        // just loop.
        if (!app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapAuthProxyEndpoints();
        app.MapDashboardProxyEndpoints();
        app.MapMarketplaceProxyEndpoints();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(SkillBridge.Shared._Imports).Assembly,
                typeof(SkillBridge.Web.Client._Imports).Assembly);

        app.Run();
    }
}
