using System.Net.Http.Headers;

namespace SkillBridge.Web.Auth;

// A same-origin passthrough to SkillBridge.ApiService's /api/auth/* endpoints. This exists
// solely so SkillBridge.Web.Client (WASM, running in the browser) can reach the ApiService
// without CORS or hardcoding a real network address: WASM has no access to Aspire's
// "https+http://skillbridge-apiservice" service-discovery scheme (that resolution only runs
// server-side), but a same-origin call to this host works from both Server and WASM render
// modes uniformly. The request/response bodies (and, for change-password, the caller's own
// Authorization header) are forwarded verbatim.
public static class ApiProxyEndpoints
{
    public static IEndpointRouteBuilder MapAuthProxyEndpoints(this IEndpointRouteBuilder app)
    {
        // AllowAnonymous is required here: Program.cs registers AddAuthorizationCore() (for
        // Blazor's own AuthorizeView/AuthorizeRouteView), and ASP.NET Core's minimal-hosting
        // model auto-inserts the server-side AuthorizationMiddleware once any authorization
        // services are registered — without this, these intentionally-anonymous endpoints
        // would get an authorization check enforced with no IAuthenticationService configured
        // to challenge with, which throws rather than just denying. This proxy doesn't
        // validate the token itself either way — it just relays whatever Authorization header
        // came in, and the real ApiService is the one that actually validates it.
        var group = app.MapGroup("/api/auth").AllowAnonymous();
        group.MapPost("/register", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/register"));
        group.MapPost("/login", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/login"));
        group.MapPost("/refresh", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/refresh"));
        group.MapPost("/logout", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/logout"));
        group.MapPost("/forgot-password", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/forgot-password"));
        group.MapPost("/reset-password", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/reset-password"));
        group.MapPost("/change-password", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/change-password"));
        group.MapPost("/roles", (HttpContext http, IHttpClientFactory f) => ForwardAsync(http, f, HttpMethod.Post, "/api/auth/roles"));
        return app;
    }

    // Same reasoning as MapAuthProxyEndpoints above: WASM can't reach the ApiService's
    // service-discovery scheme directly, so this same-origin GET passthrough exists purely to
    // relay the caller's bearer token to the real /api/dashboard endpoint. The incoming query
    // string (?role=X, the active-role selector) is forwarded through verbatim.
    public static IEndpointRouteBuilder MapDashboardProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", (HttpContext http, IHttpClientFactory f) =>
                ForwardAsync(http, f, HttpMethod.Get, $"/api/dashboard{http.Request.QueryString}"))
            .AllowAnonymous();
        return app;
    }

    // Same reasoning again, for the write-side actions behind each role's dashboard.
    public static IEndpointRouteBuilder MapMarketplaceProxyEndpoints(this IEndpointRouteBuilder app)
    {
        var courses = app.MapGroup("/api/courses").AllowAnonymous();
        courses.MapGet("/", (HttpContext http, IHttpClientFactory f) =>
            ForwardAsync(http, f, HttpMethod.Get, "/api/courses"));
        courses.MapPost("/", (HttpContext http, IHttpClientFactory f) =>
            ForwardAsync(http, f, HttpMethod.Post, "/api/courses"));
        courses.MapPost("/{courseId}/enroll", (HttpContext http, IHttpClientFactory f, string courseId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/courses/{courseId}/enroll"));
        courses.MapPost("/{courseId}/materials", (HttpContext http, IHttpClientFactory f, string courseId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/courses/{courseId}/materials"));
        courses.MapPost("/materials/{materialId}/publish", (HttpContext http, IHttpClientFactory f, string materialId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/courses/materials/{materialId}/publish"));
        courses.MapPost("/materials/{materialId}/unpublish", (HttpContext http, IHttpClientFactory f, string materialId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/courses/materials/{materialId}/unpublish"));

        var jobs = app.MapGroup("/api/jobs").AllowAnonymous();
        jobs.MapGet("/", (HttpContext http, IHttpClientFactory f) =>
            ForwardAsync(http, f, HttpMethod.Get, "/api/jobs"));
        jobs.MapGet("/mine", (HttpContext http, IHttpClientFactory f) =>
            ForwardAsync(http, f, HttpMethod.Get, "/api/jobs/mine"));
        jobs.MapPost("/", (HttpContext http, IHttpClientFactory f) =>
            ForwardAsync(http, f, HttpMethod.Post, "/api/jobs"));
        jobs.MapPost("/{jobPostingId}/apply", (HttpContext http, IHttpClientFactory f, string jobPostingId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/jobs/{jobPostingId}/apply"));
        jobs.MapPost("/applications/{applicationId}/advance", (HttpContext http, IHttpClientFactory f, string applicationId) =>
            ForwardAsync(http, f, HttpMethod.Post, $"/api/jobs/applications/{applicationId}/advance"));

        return app;
    }

    private static async Task ForwardAsync(HttpContext http, IHttpClientFactory httpClientFactory, HttpMethod method, string upstreamPath)
    {
        var client = httpClientFactory.CreateClient("ApiService");
        using var upstreamRequest = new HttpRequestMessage(method, upstreamPath);

        if (method != HttpMethod.Get)
        {
            var content = new StreamContent(http.Request.Body);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(http.Request.ContentType ?? "application/json");
            upstreamRequest.Content = content;
        }

        if (http.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            upstreamRequest.Headers.TryAddWithoutValidation("Authorization", (string?[])authHeader!);
        }

        var response = await client.SendAsync(upstreamRequest, http.RequestAborted);
        http.Response.StatusCode = (int)response.StatusCode;

        // Some upstream responses (logout, forgot-password, change-password) are 204 No
        // Content — ASP.NET Core forbids writing any bytes to the body for a no-body status,
        // even zero bytes, so only forward a body when the upstream actually sent one.
        if (response.Content.Headers.ContentLength is > 0)
        {
            http.Response.ContentType = "application/json";
            await response.Content.CopyToAsync(http.Response.Body, http.RequestAborted);
        }
    }
}
