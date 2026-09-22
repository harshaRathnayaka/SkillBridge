using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using SkillBridge.Shared.Services;
using SkillBridge.Services;

namespace SkillBridge;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add device-specific services used by the SkillBridge.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddSingleton<IAuthTokenStore, AuthTokenStore>();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();

        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiConfig.BaseUrl);
        })
#if DEBUG
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Dev-only: trusts the ASP.NET Core HTTPS dev certificate, which isn't in the
            // device/emulator's trust store. Never do this in a release build.
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
#endif
        ;

        builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiConfig.BaseUrl);
        })
#if DEBUG
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
#endif
        ;

        builder.Services.AddHttpClient<IMarketplaceApiClient, MarketplaceApiClient>(client =>
        {
            client.BaseAddress = new Uri(ApiConfig.BaseUrl);
        })
#if DEBUG
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
#endif
        ;

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
