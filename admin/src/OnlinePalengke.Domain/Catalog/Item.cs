namespace OnlinePalengke.Domain.Catalog;

/// <summary>
/// An item master row — "Pork belly", "Whole chicken", "Kangkong".
/// </summary>
/// <remarks>
/// Deliberately carries no price anywhere: prices only ever exist on a partner's quote
/// (Epic 5/6). This is the admin-curated vocabulary a customer's shopping list is built
/// from and a partner declares which of these it carries.
/// </remarks>
public sealed class Item
{
    public long Id { get; init; }

    public required long CategoryId { get; init; }

    public required string Name { get; init; }

    /// <summary>Nullable — an item can exist before an admin has uploaded a photo for it.</summary>
    public long? MediaAssetId { get; init; }

    /// <summary>
    /// Used to compute the rider fee's weight surcharge for items sold by piece or bundle
    /// rather than by weight directly (decision 12 in the product plan).
    /// </summary>
    public required decimal WeightPerUnitKg { get; init; }

    public required bool IsActive { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
