namespace OnlinePalengke.Application.Auth;

/// <summary>
/// Wire DTOs for the four public auth endpoints, admin login, and the admin-only OTP
/// bypass setting.
/// </summary>
/// <remarks>
/// Property names here are load-bearing: they are matched exactly (as
/// camelCase, via System.Text.Json's default naming policy) against the
/// Dart DTOs in <c>shared/palengke_core/lib/src/auth/auth_dtos.dart</c>.
/// Renaming a property here without renaming it there breaks every client
/// silently — <c>AuthSessionDto.fromJson</c> on the Dart side simply reads
/// null and throws a <c>FormatException</c> rather than failing to compile.
/// </remarks>
public sealed record RequestOtpRequest(string PhoneNumber);

/// <param name="ChallengeId">
/// Always null today — codes are keyed by phone, not a separate challenge
/// handle. The field exists because the client tolerates and round-trips it;
/// nothing server-side currently requires it.
/// </param>
public sealed record RequestOtpResponse(
    string? ChallengeId,
    int CodeLength,
    int ExpiresInSeconds,
    int ResendAfterSeconds);

public sealed record VerifyOtpRequest(string PhoneNumber, string Code, string? ChallengeId);

public sealed record RefreshRequest(string RefreshToken);

public sealed record SignOutRequest(string RefreshToken);

/// <param name="AccessTokenExpiresAt">
/// UTC. Named without the codebase's usual "...Utc" suffix because this
/// exact property name is what the Dart client's <c>AuthSessionDto.fromJson</c>
/// looks for first — see the type-level remarks.
/// </param>
public sealed record AuthSessionResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthUserResponse User);

public sealed record AuthUserResponse(long Id, string PhoneNumber, string? DisplayName, IReadOnlyList<string> Roles);

public sealed record AdminLoginRequest(string Email, string Password);

public sealed record AdminLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AdminUserResponse User);

public sealed record AdminUserResponse(long Id, string Email);

public sealed record SetOtpBypassRequest(bool BypassEnabled);

/// <param name="UpdatedAtUtc">Null when no admin has ever changed the setting.</param>
public sealed record OtpBypassSettingsResponse(bool BypassEnabled, DateTime? UpdatedAtUtc);
