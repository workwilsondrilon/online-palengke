namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// Which app a user signs into. One user has exactly one role — a person who is both
/// a stall owner and a customer holds two accounts, which keeps authorization simple
/// and the ledger unambiguous about who is being paid.
/// </summary>
/// <remarks>
/// Persisted as snake_case strings in <c>users.role</c>, guarded by a CHECK constraint.
/// </remarks>
public enum UserRole
{
    Customer,
    Partner,
    Rider,
    Admin,
}

/// <summary>Account-level state, independent of KYC verification.</summary>
public enum UserStatus
{
    Active,
    Suspended,
    Deleted,
}

/// <summary>
/// Verification state of a stall vendor. <see cref="Verified"/> is a hard gate: a
/// partner in any other state receives no quote requests at all.
/// </summary>
public enum PartnerStatus
{
    /// <summary>Registered but has not submitted the required documents yet.</summary>
    PendingKyc,

    /// <summary>Documents submitted, awaiting an admin reviewer.</summary>
    UnderReview,

    /// <summary>Every required document for this role and category is approved and unexpired.</summary>
    Verified,

    /// <summary>Was verified, but a document lapsed or an operator intervened.</summary>
    Suspended,

    /// <summary>Review failed. Requires resubmission to re-enter the queue.</summary>
    Rejected,
}

/// <summary>
/// Verification state of a rider. Mirrors <see cref="PartnerStatus"/> deliberately —
/// both are gated the same way, and <see cref="Verified"/> is required before a rider
/// can be assigned to a delivery run.
/// </summary>
public enum RiderStatus
{
    PendingKyc,
    UnderReview,
    Verified,
    Suspended,
    Rejected,
}
