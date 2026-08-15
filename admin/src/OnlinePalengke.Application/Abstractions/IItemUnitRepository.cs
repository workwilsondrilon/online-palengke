using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="ItemUnit"/> rows — which units an item may be ordered in.</summary>
public interface IItemUnitRepository
{
    Task<IReadOnlyList<ItemUnit>> ListForItemAsync(long itemId, CancellationToken cancellationToken = default);

    Task<long> InsertAsync(ItemUnit itemUnit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every unit assignment for an item. The service layer always follows this
    /// with a fresh insert of the full replacement set inside the same transaction — a
    /// delete-then-reinsert is how "at most one default per item" (a rule the database
    /// cannot express) stays true without a separate "clear the old default" step.
    /// </summary>
    Task DeleteForItemAsync(long itemId, CancellationToken cancellationToken = default);
}
