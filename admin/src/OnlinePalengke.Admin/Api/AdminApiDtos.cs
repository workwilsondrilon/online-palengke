namespace OnlinePalengke.Admin.Api;

/// <summary>
/// Wire DTOs for the calls <see cref="AdminApiClient"/> makes today. Deliberately not
/// shared with <c>OnlinePalengke.Application</c>'s own DTOs of (almost) the same shape —
/// Admin talks to the API over HTTP like any other client, it does not reference the
/// Application layer (see the architecture note in HANDOFF.md), so it keeps its own copy,
/// the same way the Flutter apps keep their own Dart DTOs.
/// </summary>
public sealed record AdminLoginRequest(string Email, string Password);

public sealed record AdminLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AdminUserDto User);

public sealed record AdminUserDto(long Id, string Email);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>The subset of <c>/api/auth/refresh</c>'s response an admin session needs.</summary>
public sealed record RefreshedSession(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);

/// <summary>Mirrors the RFC 7807 shape <c>GlobalExceptionHandler</c> writes, plus the API's <c>errorCode</c> extension.</summary>
public sealed record ProblemDetailsDto(
    string? Title,
    string? Detail,
    int? Status,
    string? ErrorCode,
    Dictionary<string, string[]>? Errors);
