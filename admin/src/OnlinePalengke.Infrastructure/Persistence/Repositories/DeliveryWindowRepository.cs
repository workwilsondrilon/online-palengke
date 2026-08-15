using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper repository for <see cref="DeliveryWindow"/>.
/// </summary>
/// <remarks>
/// <see cref="DeliveryWindow.StartsAt"/> / <see cref="EndsAt"/> are <see cref="TimeOnly"/>
/// in the domain but are never bound to Dapper as <see cref="TimeOnly"/> directly.
/// Verified against this Dapper/MySqlConnector version pair: passing a
/// <see cref="TimeOnly"/> query parameter throws outright (Dapper has no built-in DbType
/// mapping for it), which is safe — but reading a MySQL <c>TIME</c> column straight into a
/// <see cref="TimeOnly"/> property does something worse: it does not throw, it silently
/// returns midnight regardless of the stored value. Both directions go through
/// <see cref="TimeSpan"/> instead, which this driver pair maps correctly, with the
/// conversion to/from <see cref="TimeOnly"/> done explicitly in this file.
/// </remarks>
public sealed class DeliveryWindowRepository(DbSession session) : IDeliveryWindowRepository
{
    private const string SelectColumns =
        "id, market_id, label, starts_at, ends_at, cutoff_offset_minutes, capacity, is_active, created_at, updated_at";

    private const string SelectForMarketSql =
        $"SELECT {SelectColumns} FROM delivery_windows WHERE market_id = @MarketId ORDER BY starts_at;";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM delivery_windows WHERE id = @Id;";

    private const string InsertSql = """
        INSERT INTO delivery_windows
            (market_id, label, starts_at, ends_at, cutoff_offset_minutes, capacity, is_active, created_at, updated_at)
        VALUES
            (@MarketId, @Label, @StartsAt, @EndsAt, @CutoffOffsetMinutes, @Capacity, @IsActive, @CreatedAtUtc, @UpdatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string UpdateSql = """
        UPDATE delivery_windows
        SET label = @Label, starts_at = @StartsAt, ends_at = @EndsAt,
            cutoff_offset_minutes = @CutoffOffsetMinutes, capacity = @Capacity,
            is_active = @IsActive, updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM delivery_windows WHERE id = @Id;";

    public async Task<IReadOnlyList<DeliveryWindow>> ListForMarketAsync(
        long marketId,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DeliveryWindowRow>(new CommandDefinition(
            SelectForMarketSql,
            new { MarketId = marketId },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<DeliveryWindow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DeliveryWindowRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertAsync(DeliveryWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                window.MarketId,
                window.Label,
                StartsAt = window.StartsAt.ToTimeSpan(),
                EndsAt = window.EndsAt.ToTimeSpan(),
                window.CutoffOffsetMinutes,
                window.Capacity,
                window.IsActive,
                window.CreatedAtUtc,
                window.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(DeliveryWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                window.Id,
                window.Label,
                StartsAt = window.StartsAt.ToTimeSpan(),
                EndsAt = window.EndsAt.ToTimeSpan(),
                window.CutoffOffsetMinutes,
                window.Capacity,
                window.IsActive,
                window.UpdatedAtUtc,
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

    private sealed class DeliveryWindowRow
    {
        public long Id { get; init; }

        public long MarketId { get; init; }

        public string Label { get; init; } = string.Empty;

        public TimeSpan StartsAt { get; init; }

        public TimeSpan EndsAt { get; init; }

        public int CutoffOffsetMinutes { get; init; }

        public int Capacity { get; init; }

        public bool IsActive { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public DeliveryWindow ToDomain() => new()
        {
            Id = Id,
            MarketId = MarketId,
            Label = Label,
            StartsAt = TimeOnly.FromTimeSpan(StartsAt),
            EndsAt = TimeOnly.FromTimeSpan(EndsAt),
            CutoffOffsetMinutes = CutoffOffsetMinutes,
            Capacity = Capacity,
            IsActive = IsActive,
            CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
