using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Market"/> rows.</summary>
public interface IMarketRepository
{
    Task<IReadOnlyList<Market>> ListAsync(CancellationToken cancellationToken = default);

    Task<Market?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Market market, CancellationToken cancellationToken = default);

    /// <summary>Updates every column except <see cref="Market.ServiceAreaWkt"/>. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Market market, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or clears the delivery polygon in isolation — its own repository method
    /// because the admin UI writes it from a dedicated "Save area" action on the map
    /// panel, separate from the market's other fields. <paramref name="serviceAreaWkt"/>
    /// null clears the area.
    /// </summary>
    Task<bool> UpdateServiceAreaAsync(
        long id,
        string? serviceAreaWkt,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active markets whose service area contains the given point — the eligibility check
    /// behind decision 14 ("a request only reaches markets whose polygon contains the
    /// delivery address"). Markets with no drawn service area never match.
    /// </summary>
    Task<IReadOnlyList<Market>> FindActiveContainingPointAsync(
        decimal lat,
        decimal lng,
        CancellationToken cancellationToken = default);
}
