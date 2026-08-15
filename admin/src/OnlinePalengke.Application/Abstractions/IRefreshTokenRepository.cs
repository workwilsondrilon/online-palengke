using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="RefreshToken"/> rows.</summary>
public interface IRefreshTokenRepository
{
    Task<long> InsertAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task RevokeAsync(long id, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}
