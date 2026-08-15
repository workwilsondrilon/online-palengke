namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// A short-lived phone verification code.
/// </summary>
/// <remarks>
/// Keyed on phone rather than a user id: the code is issued before the
/// server knows whether this phone belongs to an existing user or is about
/// to create one. Only <see cref="CodeHash"/> is ever stored — a database
/// leak must not hand an attacker a working code, so a slow, salted hash
/// (bcrypt) is used deliberately rather than a fast hash, despite the code
/// itself being short-lived: six digits is only a million possibilities,
/// and a fast hash offers essentially no resistance to offline brute force.
/// </remarks>
public sealed class OtpCode
{
    /// <summary>Attempts allowed before a code is permanently invalidated and a new one must be requested.</summary>
    public const int MaxAttempts = 5;

    /// <summary>How long a code stays valid after issue.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public long Id { get; init; }

    /// <summary>E.164. The OTP row's identity key — see the type-level remarks.</summary>
    public required string Phone { get; init; }

    public required string CodeHash { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public required DateTime CreatedAtUtc { get; init; }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsConsumed => ConsumedAtUtc is not null;

    public bool AttemptsExhausted => AttemptCount >= MaxAttempts;

    /// <summary>
    /// Whether this row can still be checked against a submitted code — not
    /// expired, not already used, and has attempts remaining.
    /// </summary>
    public bool CanAttempt(DateTime nowUtc) => !IsExpired(nowUtc) && !IsConsumed && !AttemptsExhausted;

    public void RecordFailedAttempt() => AttemptCount++;

    public void MarkConsumed(DateTime nowUtc) => ConsumedAtUtc = nowUtc;
}
