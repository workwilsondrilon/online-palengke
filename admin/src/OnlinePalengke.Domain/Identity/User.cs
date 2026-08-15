namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// An account. Mobile users are identified by phone number and authenticate with an
/// OTP; admin users are identified by email and authenticate with a password. A row
/// always carries at least one of the two, enforced by a CHECK constraint in the schema.
/// </summary>
public sealed class User
{
    public long Id { get; init; }

    /// <summary>E.164 mobile number. Null for admin accounts.</summary>
    public string? Phone { get; init; }

    /// <summary>Null for mobile accounts.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// Admin accounts only. Mobile users have no password at all — there is nothing to
    /// leak and nothing to reset.
    /// </summary>
    public string? PasswordHash { get; init; }

    public required UserRole Role { get; init; }

    public required UserStatus Status { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Whether this account may act at all. Distinct from KYC verification, which is
    /// tracked per partner/rider and gates quoting and dispatch rather than sign-in.
    /// </summary>
    public bool CanAuthenticate => Status == UserStatus.Active;
}
