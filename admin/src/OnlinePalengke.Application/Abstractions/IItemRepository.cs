using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="Item"/> rows. Units for an item live in <see cref="IItemUnitRepository"/>.</summary>
public interface IItemRepository
{
    /// <summary>All items, optionally narrowed to one category.</summary>
    Task<IReadOnlyList<Item>> ListAsync(long? categoryId, CancellationToken cancellationToken = default);

    Task<Item?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(Item item, CancellationToken cancellationToken = default);

    /// <summary>Full-row update. Returns false if the row no longer exists.</summary>
    Task<bool> UpdateAsync(Item item, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
