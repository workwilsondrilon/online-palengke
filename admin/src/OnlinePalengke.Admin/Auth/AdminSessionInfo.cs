namespace OnlinePalengke.Admin.Auth;

/// <summary>The signed-in admin's session, as held in memory and persisted browser-side.</summary>
public sealed record AdminSessionInfo(
    long UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc);
