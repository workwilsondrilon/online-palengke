using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="DeliveryWindow"/> rows.</summary>
public interface IDeliveryWindowRepository
{
    Task<IReadOnlyList<DeliveryWindow>> ListForMarketAsync(long marketId, CancellationToken cancellationToken = default);

    Task<DeliveryWindow?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(DeliveryWindow window, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(DeliveryWindow window, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
