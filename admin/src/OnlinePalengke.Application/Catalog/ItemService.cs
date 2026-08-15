using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Catalog;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Catalog;

/// <summary>
/// Admin CRUD for the item master, including which units each item may be ordered in.
/// </summary>
/// <remarks>
/// An item's unit set is always replaced as a whole (see <see cref="UpdateItemRequest"/>),
/// never patched one row at a time — that is what makes "at most one default per item"
/// trivial to guarantee: delete every existing assignment, then insert the new set with
/// exactly one row flagged default, all inside one transaction.
/// </remarks>
public sealed class ItemService(
    IItemRepository items,
    IItemUnitRepository itemUnits,
    ICategoryRepository categories,
    IUnitRepository units,
    IMediaAssetRepository mediaAssets,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyList<ItemResponse>> ListAsync(
        long? categoryId,
        CancellationToken cancellationToken = default)
    {
        var rows = await items.ListAsync(categoryId, cancellationToken);

        // Categories and units are small, admin-curated reference tables (dozens of rows
        // at most) — loaded once here rather than once per item, to keep an admin list
        // screen from firing an N+1 query per row.
        var categoryLookup = (await categories.ListAsync(cancellationToken)).ToDictionary(c => c.Id);
        var unitLookup = (await units.ListAsync(cancellationToken)).ToDictionary(u => u.Id);

        var responses = new List<ItemResponse>(rows.Count);
        foreach (var item in rows)
        {
            var itemUnitRows = await itemUnits.ListForItemAsync(item.Id, cancellationToken);
            responses.Add(await ToResponseAsync(item, itemUnitRows, categoryLookup, unitLookup, cancellationToken));
        }

        return responses;
    }

    public async Task<ItemResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await items.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Item {id} was not found.");

        var category = await categories.GetByIdAsync(item.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Item {id} references missing category {item.CategoryId}.");

        var itemUnitRows = await itemUnits.ListForItemAsync(id, cancellationToken);
        var unitLookup = new Dictionary<long, Unit>();
        foreach (var itemUnit in itemUnitRows)
        {
            if (!unitLookup.ContainsKey(itemUnit.UnitId))
            {
                var unit = await units.GetByIdAsync(itemUnit.UnitId, cancellationToken)
                    ?? throw new InvalidOperationException($"Item {id} references missing unit {itemUnit.UnitId}.");
                unitLookup[unit.Id] = unit;
            }
        }

        return await ToResponseAsync(
            item,
            itemUnitRows,
            new Dictionary<long, Category> { [category.Id] = category },
            unitLookup,
            cancellationToken);
    }

    public async Task<ItemResponse> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireName(request.Name);
        var weight = RequireWeight(request.WeightPerUnitKg);
        var resolvedUnitIds = await RequireValidUnitSelectionAsync(request.UnitIds, request.DefaultUnitId, cancellationToken);

        _ = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new ValidationException(nameof(request.CategoryId), $"Category {request.CategoryId} does not exist.");

        var now = clock.UtcNow;
        var item = new Item
        {
            CategoryId = request.CategoryId,
            Name = name,
            MediaAssetId = request.MediaAssetId,
            WeightPerUnitKg = weight,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await unitOfWork.ExecuteAsync(async ct =>
        {
            var newId = await items.InsertAsync(item, ct);
            await InsertUnitAssignmentsAsync(newId, resolvedUnitIds, request.DefaultUnitId, ct);
            return newId;
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<ItemResponse> UpdateAsync(
        long id,
        UpdateItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await items.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Item {id} was not found.");

        var name = RequireName(request.Name);
        var weight = RequireWeight(request.WeightPerUnitKg);
        var resolvedUnitIds = await RequireValidUnitSelectionAsync(request.UnitIds, request.DefaultUnitId, cancellationToken);

        _ = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new ValidationException(nameof(request.CategoryId), $"Category {request.CategoryId} does not exist.");

        var updated = new Item
        {
            Id = id,
            CategoryId = request.CategoryId,
            Name = name,
            MediaAssetId = request.MediaAssetId,
            WeightPerUnitKg = weight,
            IsActive = request.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        await unitOfWork.ExecuteAsync(async ct =>
        {
            if (!await items.UpdateAsync(updated, ct))
            {
                throw new NotFoundException($"Item {id} was not found.");
            }

            await itemUnits.DeleteForItemAsync(id, ct);
            await InsertUnitAssignmentsAsync(id, resolvedUnitIds, request.DefaultUnitId, ct);
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await items.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Item {id} was not found.");

        await unitOfWork.ExecuteAsync(async ct =>
        {
            await itemUnits.DeleteForItemAsync(id, ct);
            if (!await items.DeleteAsync(id, ct))
            {
                throw new NotFoundException($"Item {id} was not found.");
            }
        }, cancellationToken);
    }

    private async Task InsertUnitAssignmentsAsync(
        long itemId,
        IReadOnlyList<long> unitIds,
        long defaultUnitId,
        CancellationToken cancellationToken)
    {
        foreach (var unitId in unitIds)
        {
            await itemUnits.InsertAsync(
                new ItemUnit { ItemId = itemId, UnitId = unitId, IsDefault = unitId == defaultUnitId },
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<long>> RequireValidUnitSelectionAsync(
        IReadOnlyList<long>? unitIds,
        long defaultUnitId,
        CancellationToken cancellationToken)
    {
        if (unitIds is null || unitIds.Count == 0)
        {
            throw new ValidationException(nameof(unitIds), "An item must have at least one unit.");
        }

        var distinct = unitIds.Distinct().ToList();

        foreach (var unitId in distinct)
        {
            _ = await units.GetByIdAsync(unitId, cancellationToken)
                ?? throw new ValidationException(nameof(unitIds), $"Unit {unitId} does not exist.");
        }

        if (!distinct.Contains(defaultUnitId))
        {
            throw new ValidationException(nameof(defaultUnitId), "The default unit must be one of the item's selected units.");
        }

        return distinct;
    }

    private async Task<ItemResponse> ToResponseAsync(
        Item item,
        IReadOnlyList<ItemUnit> itemUnitRows,
        IReadOnlyDictionary<long, Category> categoryLookup,
        IReadOnlyDictionary<long, Unit> unitLookup,
        CancellationToken cancellationToken)
    {
        var categoryName = categoryLookup.TryGetValue(item.CategoryId, out var category)
            ? category.Name
            : throw new InvalidOperationException($"Item {item.Id} references missing category {item.CategoryId}.");

        var unitResponses = itemUnitRows
            .Select(iu => unitLookup.TryGetValue(iu.UnitId, out var unit)
                ? new ItemUnitResponse(unit.Id, unit.Code, unit.Name, iu.IsDefault)
                : throw new InvalidOperationException($"Item {item.Id} references missing unit {iu.UnitId}."))
            .ToList();

        var imageUrl = await ResolveImageUrlAsync(item.MediaAssetId, cancellationToken);

        return new ItemResponse(
            item.Id,
            item.CategoryId,
            categoryName,
            item.Name,
            item.MediaAssetId,
            imageUrl,
            item.WeightPerUnitKg,
            item.IsActive,
            unitResponses);
    }

    private async Task<string?> ResolveImageUrlAsync(long? mediaAssetId, CancellationToken cancellationToken)
    {
        if (mediaAssetId is null)
        {
            return null;
        }

        var asset = await mediaAssets.GetByIdAsync(mediaAssetId.Value, cancellationToken);

        // A dangling or not-yet-committed reference degrades to "no photo" rather than
        // failing the whole item load — the same "decorative, not evidence" reasoning
        // behind items.media_asset_id being ON DELETE SET NULL in migration 002.
        if (asset is null || asset.State != MediaAssetState.Committed)
        {
            return null;
        }

        return storage.GetPublicUrl(asset.Bucket, asset.ObjectKey);
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "An item name is required.");
        }

        return name.Trim();
    }

    private static decimal RequireWeight(decimal weight)
    {
        if (weight <= 0)
        {
            throw new ValidationException(nameof(weight), "Weight per unit must be greater than zero.");
        }

        return weight;
    }
}
