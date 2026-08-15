using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="ItemUnit"/>.</summary>
public sealed class ItemUnitRepository(DbSession session) : IItemUnitRepository
{
    private const string SelectForItemSql =
        "SELECT id, item_id, unit_id, is_default FROM item_units WHERE item_id = @ItemId ORDER BY id;";

    private const string InsertSql = """
        INSERT INTO item_units (item_id, unit_id, is_default)
        VALUES (@ItemId, @UnitId, @IsDefault);
        SELECT LAST_INSERT_ID();
        """;

    private const string DeleteForItemSql = "DELETE FROM item_units WHERE item_id = @ItemId;";

    public async Task<IReadOnlyList<ItemUnit>> ListForItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ItemUnitRow>(new CommandDefinition(
            SelectForItemSql,
            new { ItemId = itemId },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<long> InsertAsync(ItemUnit itemUnit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemUnit);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new { itemUnit.ItemId, itemUnit.UnitId, itemUnit.IsDefault },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task DeleteForItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            DeleteForItemSql,
            new { ItemId = itemId },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class ItemUnitRow
    {
        public long Id { get; init; }

        public long ItemId { get; init; }

        public long UnitId { get; init; }

        public bool IsDefault { get; init; }

        public ItemUnit ToDomain() => new()
        {
            Id = Id,
            ItemId = ItemId,
            UnitId = UnitId,
            IsDefault = IsDefault,
        };
    }
}
