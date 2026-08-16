namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// A market stall's onboarding profile, one-to-one with a <see cref="UserRole.Partner"/>
/// user. Created by registration with only a stall name; <see cref="MarketId"/> and
/// <see cref="CategoryId"/> are assigned afterward (matching the nullable columns left
/// deferred in migration 001/002 — a partner registers before a market/category exists
/// to assign them to).
/// </summary>
/// <remarks>
/// <see cref="Status"/> starts at <see cref="PartnerStatus.PendingKyc"/> and is otherwise
/// entirely derived by <c>KycDocumentService</c> (Application layer, Epic 4 task #36) from
/// the partner's <c>kyc_documents</c> rows — nothing outside that service should set it
/// directly, the same way nothing outside <c>OrderStateService</c> mutates an order's
/// status.
/// </remarks>
public sealed class Partner
{
    public long Id { get; init; }

    public required long UserId { get; init; }

    public required string StallName { get; init; }

    public long? MarketId { get; init; }

    /// <summary>
    /// Drives which category-specific document types (e.g. a health card for Meat) are
    /// required before this partner can become <see cref="PartnerStatus.Verified"/>.
    /// </summary>
    public long? CategoryId { get; init; }

    public required PartnerStatus Status { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
