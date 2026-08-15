using Microsoft.AspNetCore.Mvc;
using OnlinePalengke.Application.Auth;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Api.Endpoints;

/// <summary>Phone-OTP, refresh, sign-out, and admin login endpoints.</summary>
/// <remarks>
/// Every endpoint mapped here is <c>AllowAnonymous</c> deliberately, even
/// where the surrounding route group applies <c>RequireAuthorization</c> —
/// nobody calling any of these has a token yet. Mapping them inside the
/// role-prefixed groups (rather than one flat <c>/api/auth/...</c> for
/// everything) is what lets the server know which role a brand-new phone
/// number should register as — see <see cref="OtpAuthService"/>'s remarks.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>Maps <c>POST /otp/request</c> and <c>POST /otp/verify</c> under a role-scoped group.</summary>
    public static RouteGroupBuilder MapOtpEndpoints(this RouteGroupBuilder group, UserRole role, string routeNamePrefix)
    {
        var auth = group.MapGroup("/auth/otp").WithTags("Auth");

        auth.MapPost("/request", async ([FromBody] RequestOtpRequest request, OtpAuthService otp, CancellationToken ct) =>
                Results.Ok(await otp.RequestOtpAsync(role, request.PhoneNumber, ct)))
            .WithName($"{routeNamePrefix}.RequestOtp")
            .WithSummary("Sends a one-time code to a Philippine mobile number.")
            .AllowAnonymous()
            .Produces<RequestOtpResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/verify", async ([FromBody] VerifyOtpRequest request, OtpAuthService otp, CancellationToken ct) =>
                Results.Ok(await otp.VerifyOtpAsync(role, request.PhoneNumber, request.Code, ct)))
            .WithName($"{routeNamePrefix}.VerifyOtp")
            .WithSummary("Exchanges a phone number + code for an access/refresh token pair.")
            .AllowAnonymous()
            .Produces<AuthSessionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>Maps the two endpoints that don't belong to any one role: refresh and sign-out.</summary>
    public static void MapGlobalAuthEndpoints(this RouteGroupBuilder apiGroup)
    {
        var auth = apiGroup.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/refresh", async ([FromBody] RefreshRequest request, OtpAuthService otp, CancellationToken ct) =>
                Results.Ok(await otp.RefreshAsync(request.RefreshToken, ct)))
            .WithName("Auth.Refresh")
            .WithSummary("Rotates a refresh token for a new access/refresh pair.")
            .AllowAnonymous()
            .Produces<AuthSessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/signout", async ([FromBody] SignOutRequest request, OtpAuthService otp, CancellationToken ct) =>
            {
                await otp.SignOutAsync(request.RefreshToken, ct);
                return Results.NoContent();
            })
            .WithName("Auth.SignOut")
            .WithSummary("Revokes a refresh token. Never fails on an already-revoked or unknown token.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>Maps <c>POST /auth/login</c> for the admin web app's email + password sign-in.</summary>
    public static void MapAdminLoginEndpoint(this RouteGroupBuilder adminGroup)
    {
        adminGroup.MapGroup("/auth")
            .WithTags("Auth")
            .MapPost("/login", async ([FromBody] AdminLoginRequest request, AdminAuthService admin, CancellationToken ct) =>
                    Results.Ok(await admin.LoginAsync(request.Email, request.Password, ct)))
                .WithName("Admin.Login")
                .WithSummary("Email + password login for the admin web app.")
                .AllowAnonymous()
                .Produces<AdminLoginResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
