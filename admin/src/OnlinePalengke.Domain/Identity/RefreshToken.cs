namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// A rotating refresh token.
/// </summary>
/// <remarks>
/// Only <see cref="TokenHash"/> is stored — the plaintext token exists solely
/// in the response body and the client's secure storage. On use, a refresh
/// token is revoked and a new one issued in its place (rotation) rather than
/// reused, so a replayed old token is a detectable signal, not just a
/// rejected one.
/// </remarks>
public sealed class RefreshToken
{
    /// <summary>How long a refresh token stays valid after issue.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public long Id { get; init; }

    public required long UserId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; private set; }

    public required DateTime CreatedAtUtc { get; init; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsUsable(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}
