using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="Item"/>. Units live in <see cref="ItemUnitRepository"/>.</summary>
public sealed class ItemRepository(DbSession session) : IItemRepository
{
    private const string SelectColumns =
        "id, category_id, name, media_asset_id, weight_per_unit_kg, is_active, created_at, updated_at";

    private const string SelectAllSql =
        $"SELECT {SelectColumns} FROM items ORDER BY name;";

    private const string SelectByCategorySql =
        $"SELECT {SelectColumns} FROM items WHERE category_id = @CategoryId ORDER BY name;";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM items WHERE id = @Id;";

    private const string InsertSql = """
        INSERT INTO items (category_id, name, media_asset_id, weight_per_unit_kg, is_active, created_at, updated_at)
        VALUES (@CategoryId, @Name, @MediaAssetId, @WeightPerUnitKg, @IsActive, @CreatedAtUtc, @UpdatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string UpdateSql = """
        UPDATE items
        SET category_id = @CategoryId, name = @Name, media_asset_id = @MediaAssetId,
            weight_per_unit_kg = @WeightPerUnitKg, is_active = @IsActive, updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM items WHERE id = @Id;";

    public async Task<IReadOnlyList<Item>> ListAsync(long? categoryId, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(
            categoryId is null ? SelectAllSql : SelectByCategorySql,
            categoryId is null ? null : new { CategoryId = categoryId.Value },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Item?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertAsync(Item item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                item.CategoryId,
                item.Name,
                item.MediaAssetId,
                item.WeightPerUnitKg,
                item.IsActive,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                item.Id,
                item.CategoryId,
                item.Name,
                item.MediaAssetId,
                item.WeightPerUnitKg,
                item.IsActive,
                item.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    private sealed class ItemRow
    {
        public long Id { get; init; }

        public long CategoryId { get; init; }

        public string Name { get; init; } = string.Empty;

        public long? MediaAssetId { get; init; }

        public decimal WeightPerUnitKg { get; init; }

        public bool IsActive { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public Item ToDomain() => new()
        {
            Id = Id,
            CategoryId = CategoryId,
            Name = Name,
            MediaAssetId = MediaAssetId,
            WeightPerUnitKg = WeightPerUnitKg,
            IsActive = IsActive,
            CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
