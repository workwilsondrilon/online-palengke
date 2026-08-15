namespace OnlinePalengke.Domain.Catalog;

/// <summary>
/// One valid unit of measure for an item — pork can be ordered "by kg" or "by piece", each
/// a separate row here.
/// </summary>
public sealed class ItemUnit
{
    public long Id { get; init; }

    public required long ItemId { get; init; }

    public required long UnitId { get; init; }

    /// <summary>
    /// The unit shown by default when building a shopping-list line for this item. Exactly
    /// one <see cref="ItemUnit"/> per item may have this set — enforced by the repository,
    /// not the database, since MySQL has no native "at most one true" constraint.
    /// </summary>
    public required bool IsDefault { get; init; }
}
