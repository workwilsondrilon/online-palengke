using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="MediaAsset"/> rows.</summary>
public interface IMediaAssetRepository
{
    /// <summary>Inserts a new asset in the pending state and returns its generated id.</summary>
    Task<long> InsertAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    Task<MediaAsset?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Moves an asset to committed. Returns false if the row no longer exists.</summary>
    Task<bool> MarkCommittedAsync(long id, DateTime committedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending assets older than the cutoff — presigned but never uploaded. The daily
    /// sweep deletes these so abandoned uploads do not accumulate rows forever.
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> GetStalePendingAsync(
        DateTime olderThanUtc,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
