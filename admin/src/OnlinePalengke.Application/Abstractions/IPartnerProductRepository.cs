using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="PartnerProduct"/> rows.</summary>
public interface IPartnerProductRepository
{
    Task<PartnerProduct?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Null if this partner has never declared this item — backs the one-declaration-per-item rule.</summary>
    Task<PartnerProduct?> GetByPartnerAndItemAsync(
        long partnerId, long itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerProduct>> ListForPartnerAsync(long partnerId, CancellationToken cancellationToken = default);

    /// <summary>Every published product across every partner — the admin content feed, and later Epic 5's quote cards.</summary>
    Task<IReadOnlyList<PartnerProduct>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<long> InsertAsync(PartnerProduct product, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(PartnerProduct product, CancellationToken cancellationToken = default);
}
