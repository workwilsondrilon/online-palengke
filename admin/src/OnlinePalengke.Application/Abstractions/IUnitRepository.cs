using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Unit"/> rows.</summary>
public interface IUnitRepository
{
    Task<IReadOnlyList<Unit>> ListAsync(CancellationToken cancellationToken = default);

    Task<Unit?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Unit?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Unit unit, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Unit unit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the row. The database refuses this with a foreign-key violation while any
    /// <see cref="ItemUnit"/> still references the unit.
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
