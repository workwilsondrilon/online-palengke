namespace OnlinePalengke.Application.Catalog;

/// <summary>A catalog category for the admin category screen.</summary>
public sealed record CategoryResponse(long Id, string Name, string Slug, int SortOrder, bool IsActive);

public sealed record CreateCategoryRequest(string Name, string Slug, int SortOrder);

public sealed record UpdateCategoryRequest(string Name, string Slug, int SortOrder, bool IsActive);

/// <summary>A unit of measure for the admin units screen.</summary>
public sealed record UnitResponse(long Id, string Code, string Name, bool AllowsFractionalQuantity, bool IsActive);

public sealed record CreateUnitRequest(string Code, string Name, bool AllowsFractionalQuantity);

public sealed record UpdateUnitRequest(string Code, string Name, bool AllowsFractionalQuantity, bool IsActive);

/// <summary>One unit an item may be ordered in, as returned alongside the item.</summary>
public sealed record ItemUnitResponse(long UnitId, string UnitCode, string UnitName, bool IsDefault);

/// <summary>
/// The item master row a shopping list is built from. Deliberately carries no price
/// anywhere — prices only ever exist on a partner's quote.
/// </summary>
public sealed record ItemResponse(
    long Id,
    long CategoryId,
    string CategoryName,
    string Name,
    long? MediaAssetId,
    string? ImageUrl,
    decimal WeightPerUnitKg,
    bool IsActive,
    IReadOnlyList<ItemUnitResponse> Units);

/// <param name="UnitIds">Every unit this item may be ordered in.</param>
/// <param name="DefaultUnitId">Must be one of <paramref name="UnitIds"/>.</param>
public sealed record CreateItemRequest(
    long CategoryId,
    string Name,
    long? MediaAssetId,
    decimal WeightPerUnitKg,
    IReadOnlyList<long> UnitIds,
    long DefaultUnitId);

/// <summary>
/// Full replace: the item's unit set becomes exactly <paramref name="UnitIds"/>. There is
/// no separate "add one unit" endpoint — an admin edits the whole list at once, which
/// keeps "at most one default" trivial to enforce and matches how the edit screen presents
/// the units as one multi-select.
/// </summary>
public sealed record UpdateItemRequest(
    long CategoryId,
    string Name,
    long? MediaAssetId,
    decimal WeightPerUnitKg,
    bool IsActive,
    IReadOnlyList<long> UnitIds,
    long DefaultUnitId);
