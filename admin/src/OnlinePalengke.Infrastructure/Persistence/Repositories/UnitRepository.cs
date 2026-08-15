using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="Unit"/>.</summary>
public sealed class UnitRepository(DbSession session) : IUnitRepository
{
    private const string SelectColumns =
        "id, code, name, allows_fractional_quantity, is_active, created_at, updated_at";

    private const string SelectAllSql =
        $"SELECT {SelectColumns} FROM units ORDER BY name;";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM units WHERE id = @Id;";

    private const string SelectByCodeSql =
        $"SELECT {SelectColumns} FROM units WHERE code = @Code;";

    private const string InsertSql = """
        INSERT INTO units (code, name, allows_fractional_quantity, is_active, created_at, updated_at)
        VALUES (@Code, @Name, @AllowsFractionalQuantity, @IsActive, @CreatedAtUtc, @UpdatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string UpdateSql = """
        UPDATE units
        SET code = @Code, name = @Name, allows_fractional_quantity = @AllowsFractionalQuantity,
            is_active = @IsActive, updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM units WHERE id = @Id;";

    public async Task<IReadOnlyList<Unit>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<UnitRow>(new CommandDefinition(
            SelectAllSql,
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Unit?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UnitRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<Unit?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UnitRow>(new CommandDefinition(
            SelectByCodeSql,
            new { Code = code },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                unit.Code,
                unit.Name,
                unit.AllowsFractionalQuantity,
                unit.IsActive,
                unit.CreatedAtUtc,
                unit.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                unit.Id,
                unit.Code,
                unit.Name,
                unit.AllowsFractionalQuantity,
                unit.IsActive,
                unit.UpdatedAtUtc,
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

    private sealed class UnitRow
    {
        public long Id { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public bool AllowsFractionalQuantity { get; init; }

        public bool IsActive { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public Unit ToDomain() => new()
        {
            Id = Id,
            Code = Code,
            Name = Name,
            AllowsFractionalQuantity = AllowsFractionalQuantity,
            IsActive = IsActive,
            CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
