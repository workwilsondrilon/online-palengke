using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Kyc;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="KycDocument"/> rows.</summary>
/// <remarks>
/// A resubmission inserts a new row rather than mutating a rejected one, so an owner's
/// rejection history is preserved. Every "current state" query below therefore means "the
/// most recently submitted row for that owner and document type", not "the only row".
/// </remarks>
public interface IKycDocumentRepository
{
    Task<KycDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<KycDocument?> GetLatestForOwnerAndTypeAsync(
        UserRole ownerRole, long ownerId, long documentTypeId, CancellationToken cancellationToken = default);

    /// <summary>The latest row per document type this owner has ever submitted.</summary>
    Task<IReadOnlyList<KycDocument>> ListLatestForOwnerAsync(
        UserRole ownerRole, long ownerId, CancellationToken cancellationToken = default);

    /// <summary>Every document currently awaiting an admin decision, across every owner.</summary>
    Task<IReadOnlyList<KycDocument>> ListPendingReviewAsync(CancellationToken cancellationToken = default);

    Task<long> InsertAsync(KycDocument document, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(KycDocument document, CancellationToken cancellationToken = default);
}
