using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Rider"/> rows.</summary>
public interface IRiderRepository
{
    Task<Rider?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>One rider profile per user — the lookup a "my profile" request starts from.</summary>
    Task<Rider?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Rider>> ListAsync(RiderStatus? status = null, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Rider rider, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Rider rider, CancellationToken cancellationToken = default);
}
