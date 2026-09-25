using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Data;
using SkillBridge.ApiService.Data.Seed;
using SkillBridge.ApiService.Email;

namespace SkillBridge.ApiService.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting("auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/roles", AddRoleAsync).RequireAuthorization();

        return app;
    }

    private static readonly ErrorResponse InvalidConfirmationToken = new(["Invalid or expired confirmation code."]);

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Json(InvalidConfirmationToken, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return Results.Json(InvalidConfirmationToken, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static readonly ErrorResponse Unauthenticated = new(["Not authenticated."]);

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request, HttpContext http, UserManager<ApplicationUser> userManager)
    {
        var userId = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Json(Unauthenticated, statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Results.Json(Unauthenticated, statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Results.BadRequest(new ErrorResponse(result.Errors.Select(e => e.Description).ToArray()));
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, UserManager<ApplicationUser> userManager, IEmailSender emailSender)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var body = $"We received a request to reset your SkillBridge password.\n\n" +
                       $"Reset code: {token}\n\n" +
                       "If you didn't request this, you can safely ignore this email. This code expires in 1 hour.";
            await emailSender.SendAsync(request.Email, "Reset your SkillBridge password", body);
        }

        // Always 204, regardless of whether the email exists — same no-enumeration-leak
        // principle as the generic login-failure message.
        return Results.NoContent();
    }

    private static readonly ErrorResponse InvalidResetToken = new(["Invalid or expired reset code."]);

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Json(InvalidResetToken, statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var invalidToken = result.Errors.Any(e => e.Code == "InvalidToken");
            var errors = invalidToken ? InvalidResetToken.Errors : result.Errors.Select(e => e.Description).ToArray();
            return Results.Json(new ErrorResponse(errors), statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(RefreshRequest request, IRefreshTokenService refreshTokenService)
    {
        await refreshTokenService.RevokeAsync(request.RefreshToken, request.DeviceId);
        return Results.NoContent();
    }

    private static readonly ErrorResponse InvalidRefreshToken = new(["Refresh token is invalid, expired, or has already been used."]);

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IConfiguration configuration)
    {
        var refreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 7);
        var result = await refreshTokenService.RotateAsync(
            request.RefreshToken,
            request.DeviceId,
            DateTimeOffset.UtcNow.AddDays(refreshTokenDays),
            createdByIp: null);

        if (!result.Succeeded)
        {
            return Results.Json(InvalidRefreshToken, statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(result.UserId!);
        if (user is null)
        {
            return Results.Json(InvalidRefreshToken, statusCode: StatusCodes.Status401Unauthorized);
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
        var accessToken = tokenService.CreateAccessToken(user, roles, expiresAtUtc);

        return Results.Ok(new AuthResponse(accessToken, result.NewRawToken!, expiresAtUtc, roles.ToArray(), user.DisplayName));
    }

    private static readonly ErrorResponse InvalidCredentials = new(["Invalid email or password."]);
    private static readonly ErrorResponse AccountLocked = new(["Too many failed attempts. Try again in 15 minutes."]);

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IConfiguration configuration)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Json(InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Json(AccountLocked, statusCode: StatusCodes.Status423Locked);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Under heavy concurrent attempts against the same account (see StressTests),
            // two requests can race to update the same user row; Identity's ConcurrencyStamp
            // check on ApplicationUser makes the loser throw a DbUpdateConcurrencyException,
            // and a transient SQLite write-lock timeout under contention surfaces as the
            // broader DbUpdateException (same reasoning as RefreshTokenService.RotateAsync).
            // The failed-attempt count is best-effort under that kind of race — it
            // self-corrects on the next attempt — so a lost increment just falls back to a
            // clean 401 instead of a 500.
            try
            {
                await userManager.AccessFailedAsync(user);
            }
            catch (DbUpdateException)
            {
                return Results.Json(InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (await userManager.IsLockedOutAsync(user))
            {
                return Results.Json(AccountLocked, statusCode: StatusCodes.Status423Locked);
            }

            return Results.Json(InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
        catch (DbUpdateException)
        {
            // Same race, the other direction (e.g. two devices logging in with the correct
            // password at once) — losing the reset doesn't affect this login's own success.
        }

        var roles = await userManager.GetRolesAsync(user);

        var accessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
        var accessToken = tokenService.CreateAccessToken(user, roles, expiresAtUtc);

        var refreshTokenDaysKey = request.RememberMe ? "Jwt:RefreshTokenDaysRemembered" : "Jwt:RefreshTokenDays";
        var refreshTokenDays = configuration.GetValue(refreshTokenDaysKey, 7);
        var refreshToken = await refreshTokenService.IssueAsync(
            user.Id,
            request.DeviceId,
            request.DeviceLabel,
            DateTimeOffset.UtcNow.AddDays(refreshTokenDays),
            createdByIp: null);

        return Results.Ok(new AuthResponse(accessToken, refreshToken, expiresAtUtc, roles.ToArray(), user.DisplayName));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        var requestedRoles = request.Roles.Distinct(StringComparer.Ordinal).ToList();
        if (requestedRoles.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse(["Choose at least one role."]));
        }

        var unrecognized = requestedRoles.Where(r => !RoleSeeder.RoleNames.Contains(r, StringComparer.Ordinal)).ToList();
        if (unrecognized.Count > 0)
        {
            return Results.BadRequest(new ErrorResponse([$"'{unrecognized[0]}' is not a recognized role."]));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new ErrorResponse(createResult.Errors.Select(e => e.Description).ToArray()));
        }

        foreach (var roleName in requestedRoles)
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

        var roles = await userManager.GetRolesAsync(user);

        // Account is created and usable immediately (unchanged behavior) — confirmation is
        // tracked for future features (e.g. a "verified" trust badge) but doesn't gate login.
        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationBody = $"Welcome to SkillBridge!\n\n" +
                                $"Confirmation code: {confirmationToken}\n\n" +
                                "If you didn't create this account, you can safely ignore this email.";
        await emailSender.SendAsync(request.Email, "Confirm your SkillBridge email", confirmationBody);

        var accessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
        var accessToken = tokenService.CreateAccessToken(user, roles, expiresAtUtc);

        var refreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 7);
        var refreshToken = await refreshTokenService.IssueAsync(
            user.Id,
            request.DeviceId,
            request.DeviceLabel,
            DateTimeOffset.UtcNow.AddDays(refreshTokenDays),
            createdByIp: null);

        return Results.Ok(new AuthResponse(accessToken, refreshToken, expiresAtUtc, roles.ToArray(), user.DisplayName));
    }

    private static readonly ErrorResponse AlreadyHasRole = new(["You already have this role."]);

    private static async Task<IResult> AddRoleAsync(
        AddRoleRequest request,
        HttpContext http,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IConfiguration configuration)
    {
        var userId = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Json(Unauthenticated, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!RoleSeeder.RoleNames.Contains(request.Role, StringComparer.Ordinal))
        {
            return Results.BadRequest(new ErrorResponse([$"'{request.Role}' is not a recognized role."]));
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Results.Json(Unauthenticated, statusCode: StatusCodes.Status401Unauthorized);
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (existingRoles.Contains(request.Role, StringComparer.Ordinal))
        {
            return Results.Json(AlreadyHasRole, statusCode: StatusCodes.Status409Conflict);
        }

        await userManager.AddToRoleAsync(user, request.Role);
        var roles = await userManager.GetRolesAsync(user);

        // Only the access token is re-issued — it's the only place roles are baked in. The
        // caller's existing refresh token is untouched: RefreshAsync/LoginAsync already look up
        // roles fresh from the database on every call, so it needs no update of its own.
        var accessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
        var accessToken = tokenService.CreateAccessToken(user, roles, expiresAtUtc);

        return Results.Ok(new AddRoleResponse(accessToken, expiresAtUtc, roles.ToArray()));
    }
}
