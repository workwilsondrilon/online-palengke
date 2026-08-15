using System.Globalization;
using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper repository for <see cref="Market"/> — the one repository in the solution that
/// crosses into geometry. Two things about this file are load-bearing, both verified
/// against the real local server while migration 002 was written and re-verified here:
/// <list type="bullet">
/// <item><c>service_area</c> is read with <c>ST_AsText</c> and written with
/// <c>ST_GeomFromText(..., 4326)</c>. <c>ST_GeomFromText(NULL, 4326)</c> itself returns
/// NULL (confirmed directly), so there is no need for a CASE expression to special-case a
/// cleared service area.</item>
/// <item>WKT coordinate order under SRID 4326 in MySQL 8 is latitude, longitude — the
/// reverse of GeoJSON. <see cref="Market.ServiceAreaWkt"/> is already in that order by the
/// time it reaches this repository (the Application layer converts through
/// <see cref="PolygonWkt"/>), and <see cref="BuildPointWkt"/> below follows the same order
/// for the eligibility query's point.</item>
/// </list>
/// </summary>
public sealed class MarketRepository(DbSession session) : IMarketRepository
{
    private const string SelectColumns =
        "id, name, address, city, province, lat, lng, ST_AsText(service_area) AS service_area, status, created_at, updated_at";

    private const string SelectAllSql =
        $"SELECT {SelectColumns} FROM markets ORDER BY name;";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM markets WHERE id = @Id;";

    private const string InsertSql = """
        INSERT INTO markets (name, address, city, province, lat, lng, service_area, status, created_at, updated_at)
        VALUES (@Name, @Address, @City, @Province, @Lat, @Lng, ST_GeomFromText(@ServiceAreaWkt, 4326), @Status, @CreatedAtUtc, @UpdatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    // Deliberately does not touch service_area -- that column is only ever written by
    // UpdateServiceAreaSql, matching the admin UI's separate "Save area" action.
    private const string UpdateSql = """
        UPDATE markets
        SET name = @Name, address = @Address, city = @City, province = @Province,
            lat = @Lat, lng = @Lng, status = @Status, updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string UpdateServiceAreaSql = """
        UPDATE markets
        SET service_area = ST_GeomFromText(@ServiceAreaWkt, 4326), updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM markets WHERE id = @Id;";

    private const string SelectActiveContainingPointSql = $"""
        SELECT {SelectColumns}
        FROM markets
        WHERE status = 'active'
          AND service_area IS NOT NULL
          AND ST_Contains(service_area, ST_GeomFromText(@PointWkt, 4326))
        ORDER BY name;
        """;

    public async Task<IReadOnlyList<Market>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MarketRow>(new CommandDefinition(
            SelectAllSql,
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Market?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MarketRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertAsync(Market market, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(market);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                market.Name,
                market.Address,
                market.City,
                market.Province,
                market.Lat,
                market.Lng,
                market.ServiceAreaWkt,
                Status = Naming.ToDbValue(market.Status),
                market.CreatedAtUtc,
                market.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Market market, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(market);

        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                market.Id,
                market.Name,
                market.Address,
                market.City,
                market.Province,
                market.Lat,
                market.Lng,
                Status = Naming.ToDbValue(market.Status),
                market.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> UpdateServiceAreaAsync(
        long id,
        string? serviceAreaWkt,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateServiceAreaSql,
            new { Id = id, ServiceAreaWkt = serviceAreaWkt, UpdatedAtUtc = updatedAtUtc },
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

    public async Task<IReadOnlyList<Market>> FindActiveContainingPointAsync(
        decimal lat,
        decimal lng,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MarketRow>(new CommandDefinition(
            SelectActiveContainingPointSql,
            new { PointWkt = BuildPointWkt(lat, lng) },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    /// <summary>Builds a WKT POINT in the "latitude longitude" order SRID 4326 expects — see this file's header.</summary>
    private static string BuildPointWkt(decimal lat, decimal lng) =>
        $"POINT({lat.ToString(CultureInfo.InvariantCulture)} {lng.ToString(CultureInfo.InvariantCulture)})";

    private sealed class MarketRow
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Address { get; init; } = string.Empty;

        public string City { get; init; } = string.Empty;

        public string Province { get; init; } = string.Empty;

        public decimal Lat { get; init; }

        public decimal Lng { get; init; }

        public string? ServiceArea { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public Market ToDomain() => new()
        {
            Id = Id,
            Name = Name,
            Address = Address,
            City = City,
            Province = Province,
            Lat = Lat,
            Lng = Lng,
            ServiceAreaWkt = ServiceArea,
            Status = Naming.FromDbValue<MarketStatus>(Status),
            CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
