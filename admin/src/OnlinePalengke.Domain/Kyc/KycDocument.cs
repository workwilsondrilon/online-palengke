using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Domain.Kyc;

/// <summary>
/// One submission of one document against one <see cref="DocumentType"/>, by a partner or
/// rider.
/// </summary>
/// <remarks>
/// A resubmission after a rejection inserts a new row rather than mutating the rejected
/// one — the rejection reason and reviewer stay on the historical record, and a
/// completeness check (see <see cref="DocumentType"/>) always reads the newest row per
/// <c>(OwnerRole, OwnerId, DocumentTypeId)</c>. This means <c>owner_id</c> alone is not a
/// unique key against <c>document_type_id</c>; the Infrastructure repository (task #37)
/// is responsible for the "latest per owner+type" query.
/// </remarks>
public sealed class KycDocument
{
    public long Id { get; init; }

    /// <summary>Which profile table <see cref="OwnerId"/> points at — always Partner or Rider.</summary>
    public required UserRole OwnerRole { get; init; }

    /// <summary>The partner or rider id this document belongs to (not the user id).</summary>
    public required long OwnerId { get; init; }

    public required long DocumentTypeId { get; init; }

    public required long MediaAssetId { get; init; }

    public required KycDocumentStatus Status { get; init; }

    public required DateTime SubmittedAtUtc { get; init; }

    /// <summary>The admin user who approved or rejected this submission.</summary>
    public long? ReviewedBy { get; init; }

    public DateTime? ReviewedAtUtc { get; init; }

    public string? RejectionReason { get; init; }

    /// <summary>
    /// Set on approval when <see cref="DocumentType.RequiresExpiry"/> is true. Read by the
    /// daily expiry job to warn at 30/7 days and to expire the document on lapse.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; init; }
}
