namespace OnlinePalengke.Domain.Markets;

/// <summary>
/// A wet market in the network. A market is the unit everything else batches by: partners
/// belong to one, quote requests are broadcast per market, and a delivery run is one
/// market plus one window plus one date.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Lat"/>/<see cref="Lng"/> are the market's own pin, not the service area.
/// They are the origin point of the rider fee's distance calculation (market → customer,
/// decision 12), which is why they are <c>decimal</c> and not <c>double</c>: the same
/// exactness argument as <c>customer_addresses.lat/lng</c>, since money is derived from
/// them.
/// </para>
/// <para>
/// The service area itself lives in <see cref="ServiceAreaWkt"/> rather than in a
/// geometry type, because the polygon crosses the Dapper boundary as WKT —
/// <c>ST_AsText</c> on read, <c>ST_GeomFromText(:wkt, 4326)</c> on write. Keeping the
/// Domain in the same representation means there is exactly one conversion in the system
/// (WKT ↔ GeoJSON, at the Admin/Leaflet edge) instead of two.
/// </para>
/// </remarks>
public sealed class Market
{
    public long Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Street address of the market itself, for display and for rider wayfinding.</summary>
    public required string Address { get; init; }

    /// <summary>
    /// City and province are stored as their own columns rather than being parsed back out
    /// of <see cref="Address"/>: the admin market list groups and filters on them, and the
    /// same market name recurs across provinces.
    /// </summary>
    public required string City { get; init; }

    /// <inheritdoc cref="City"/>
    public required string Province { get; init; }

    public required decimal Lat { get; init; }

    public required decimal Lng { get; init; }

    /// <summary>
    /// The delivery service polygon as WKT in SRID 4326 — e.g.
    /// <c>POLYGON((120.985 14.655, 121.015 14.655, ..., 120.985 14.655))</c>, longitude
    /// first, ring explicitly closed.
    /// </summary>
    /// <remarks>
    /// Nullable, and the column is nullable to match. The plan's data-model sketch had this
    /// <c>NOT NULL</c>, but the admin flow creates the market first and draws the polygon
    /// afterwards on the market's own detail page — requiring a polygon at insert time
    /// would mean either a two-phase write or forcing an operator to draw an area before
    /// they have somewhere to draw it on. A market with no area simply matches no address,
    /// which is the correct behaviour for an <see cref="MarketStatus.Onboarding"/> market
    /// anyway.
    /// </remarks>
    public string? ServiceAreaWkt { get; init; }

    /// <summary>True once an operator has drawn and saved a service area.</summary>
    public bool HasServiceArea => !string.IsNullOrWhiteSpace(ServiceAreaWkt);

    public required MarketStatus Status { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
