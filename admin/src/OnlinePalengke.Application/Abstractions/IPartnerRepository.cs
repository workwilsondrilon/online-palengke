using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Partner"/> rows.</summary>
public interface IPartnerRepository
{
    Task<Partner?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>One partner profile per user — the lookup a "my profile" request starts from.</summary>
    Task<Partner?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Partner>> ListAsync(PartnerStatus? status = null, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Partner partner, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Partner partner, CancellationToken cancellationToken = default);
}
