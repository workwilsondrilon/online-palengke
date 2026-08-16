using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Domain.Kyc;

/// <summary>
/// An admin-configurable KYC requirement — e.g. "valid ID" for every partner, or "health
/// card" for partners in the Meat category specifically.
/// </summary>
/// <remarks>
/// <see cref="AppliesToRole"/> reuses <see cref="UserRole"/> rather than a new enum; the
/// Application layer rejects anything other than <see cref="UserRole.Partner"/> or
/// <see cref="UserRole.Rider"/>, since those are the only roles this table has ever meant
/// to describe. <see cref="AppliesToCategoryId"/> is null for a role-wide requirement and
/// set for a category-specific add-on — a partner/rider becomes verified only once every
/// <see cref="IsRequired"/> row matching their role (and, for a partner, their category)
/// has an approved, unexpired <see cref="KycDocument"/>.
/// </remarks>
public sealed class DocumentType
{
    public long Id { get; init; }

    /// <summary>Stable machine identifier — e.g. "partner_valid_id". Never reused after deletion.</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required UserRole AppliesToRole { get; init; }

    public long? AppliesToCategoryId { get; init; }

    public required bool IsRequired { get; init; }

    /// <summary>Whether a submitted document needs an <c>ExpiresAt</c> captured on approval.</summary>
    public required bool RequiresExpiry { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
