namespace SkillBridge.Shared.Services;

public record AuthPayload(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc, string[] Roles, string DisplayName);

public record AuthApiResult(bool Succeeded, AuthPayload? Payload, string[] Errors)
{
    public static AuthApiResult Success(AuthPayload payload) => new(true, payload, []);
    public static AuthApiResult Failure(string[] errors) => new(false, null, errors);
}

public record SimpleApiResult(bool Succeeded, string[] Errors)
{
    public static SimpleApiResult Success() => new(true, []);
    public static SimpleApiResult Failure(string[] errors) => new(false, errors);
}

public record AddRolePayload(string AccessToken, DateTimeOffset ExpiresAtUtc, string[] Roles);

public record AddRoleApiResult(bool Succeeded, AddRolePayload? Payload, string[] Errors)
{
    public static AddRoleApiResult Success(AddRolePayload payload) => new(true, payload, []);
    public static AddRoleApiResult Failure(string[] errors) => new(false, null, errors);
}

public interface IAuthApiClient
{
    Task<AuthApiResult> RegisterAsync(
        string email, string password, string displayName, IReadOnlyList<string> roles, string deviceId,
        string? deviceLabel = null, CancellationToken cancellationToken = default);

    Task<AuthApiResult> LoginAsync(
        string email, string password, string deviceId,
        string? deviceLabel = null, bool rememberMe = false, CancellationToken cancellationToken = default);

    Task<AuthApiResult> RefreshAsync(string refreshToken, string deviceId, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, string deviceId, CancellationToken cancellationToken = default);

    Task<SimpleApiResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    Task<SimpleApiResult> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default);

    Task<SimpleApiResult> ChangePasswordAsync(
        string accessToken, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task<AddRoleApiResult> AddRoleAsync(string accessToken, string role, CancellationToken cancellationToken = default);
}
