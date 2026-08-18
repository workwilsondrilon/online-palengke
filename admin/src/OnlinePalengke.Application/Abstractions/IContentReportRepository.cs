using OnlinePalengke.Domain.Moderation;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="ContentReport"/> rows.</summary>
public interface IContentReportRepository
{
    Task<ContentReport?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentReport>> ListAsync(ContentReportStatus? status = null, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(ContentReport report, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(ContentReport report, CancellationToken cancellationToken = default);
}
