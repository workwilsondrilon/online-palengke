namespace OnlinePalengke.Domain.Catalog;

/// <summary>
/// A catalog category — Meat, Poultry, Fish &amp; Seafood, Vegetables, Dry goods.
/// </summary>
/// <remarks>
/// Categories are what a shopping-list request buckets into separate mini-RFQs (Epic 5):
/// one winning stall per category, not per item. Admin-curated, never customer- or
/// partner-created.
/// </remarks>
public sealed class Category
{
    public long Id { get; init; }

    public required string Name { get; init; }

    /// <summary>URL-safe, stable identifier — e.g. "fish-seafood". Never reused after deletion.</summary>
    public required string Slug { get; init; }

    public required int SortOrder { get; init; }

    public required bool IsActive { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
