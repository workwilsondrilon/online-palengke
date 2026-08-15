namespace OnlinePalengke.Admin.Api;

/// <summary>Wire DTOs for <c>/api/admin/categories</c>, <c>/api/admin/units</c> and <c>/api/admin/items</c>.</summary>
/// <remarks>
/// Mirrors <c>OnlinePalengke.Application.Catalog.CatalogContracts</c> but is not shared with
/// it - Admin talks to the API over HTTP like any other client, it does not reference the
/// Application layer. See <see cref="AdminLoginRequest"/>'s remarks for why.
/// </remarks>
public sealed record CategoryDto(long Id, string Name, string Slug, int SortOrder, bool IsActive);

public sealed record CreateCategoryRequest(string Name, string Slug, int SortOrder);

public sealed record UpdateCategoryRequest(string Name, string Slug, int SortOrder, bool IsActive);

public sealed record UnitDto(long Id, string Code, string Name, bool AllowsFractionalQuantity, bool IsActive);

public sealed record CreateUnitRequest(string Code, string Name, bool AllowsFractionalQuantity);

public sealed record UpdateUnitRequest(string Code, string Name, bool AllowsFractionalQuantity, bool IsActive);

public sealed record ItemUnitDto(long UnitId, string UnitCode, string UnitName, bool IsDefault);

public sealed record ItemDto(
    long Id,
    long CategoryId,
    string CategoryName,
    string Name,
    long? MediaAssetId,
    string? ImageUrl,
    decimal WeightPerUnitKg,
    bool IsActive,
    IReadOnlyList<ItemUnitDto> Units);

public sealed record CreateItemRequest(
    long CategoryId,
    string Name,
    long? MediaAssetId,
    decimal WeightPerUnitKg,
    IReadOnlyList<long> UnitIds,
    long DefaultUnitId);

public sealed record UpdateItemRequest(
    long CategoryId,
    string Name,
    long? MediaAssetId,
    decimal WeightPerUnitKg,
    bool IsActive,
    IReadOnlyList<long> UnitIds,
    long DefaultUnitId);
