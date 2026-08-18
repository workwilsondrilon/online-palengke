using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Kyc;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="DocumentType"/> rows.</summary>
public interface IDocumentTypeRepository
{
    Task<IReadOnlyList<DocumentType>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every document type a given role must consider — role-wide types plus, for a
    /// partner, the ones scoped to their specific category. Pass null for
    /// <paramref name="categoryId"/> for a rider (riders have no category concept) or for
    /// a partner who has not been assigned one yet.
    /// </summary>
    Task<IReadOnlyList<DocumentType>> ListForRoleAsync(
        UserRole role, long? categoryId, CancellationToken cancellationToken = default);

    Task<DocumentType?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<DocumentType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(DocumentType documentType, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(DocumentType documentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the row. The database refuses this with a foreign-key violation while any
    /// KYC document still references the type — the service layer turns that into a
    /// friendly conflict.
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
