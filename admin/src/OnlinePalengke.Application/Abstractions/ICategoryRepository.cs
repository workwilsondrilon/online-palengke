using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Category"/> rows.</summary>
public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the row. The database refuses this with a foreign-key violation while any
    /// item still references the category — the service layer turns that into a friendly
    /// conflict.
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
