namespace OnlinePalengke.Application.Markets;

/// <summary>A wet market for the admin markets screen.</summary>
/// <param name="ServiceAreaGeoJson">
/// The service polygon as GeoJSON — the shape Leaflet.draw consumes directly — or null if
/// no area has been drawn yet. Never WKT: WKT never crosses the Application/Api boundary,
/// only the Infrastructure/database one.
/// </param>
public sealed record MarketResponse(
    long Id,
    string Name,
    string Address,
    string City,
    string Province,
    decimal Lat,
    decimal Lng,
    string? ServiceAreaGeoJson,
    bool HasServiceArea,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateMarketRequest(string Name, string Address, string City, string Province, decimal Lat, decimal Lng);

/// <summary>Everything about a market except its service area — that is saved through <see cref="UpdateServiceAreaRequest"/>.</summary>
public sealed record UpdateMarketRequest(
    string Name,
    string Address,
    string City,
    string Province,
    decimal Lat,
    decimal Lng,
    string Status);

/// <param name="PolygonGeoJson">
/// GeoJSON Polygon from Leaflet.draw, or null to clear a previously drawn area.
/// </param>
public sealed record UpdateServiceAreaRequest(string? PolygonGeoJson);

public sealed record DeliveryWindowResponse(
    long Id,
    long MarketId,
    string Label,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int CutoffOffsetMinutes,
    int Capacity,
    bool IsActive);

public sealed record CreateDeliveryWindowRequest(
    string Label,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int CutoffOffsetMinutes,
    int Capacity);

public sealed record UpdateDeliveryWindowRequest(
    string Label,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int CutoffOffsetMinutes,
    int Capacity,
    bool IsActive);

/// <summary>A customer's candidate delivery point — usually a pinned address, not yet saved.</summary>
public sealed record EligibilityRequest(decimal Lat, decimal Lng);

public sealed record EligibleMarketResponse(long Id, string Name, string City, string Province);

/// <param name="Message">
/// A ready-to-display explanation — decision 14 requires refusal to come with a clear
/// message, not just an empty list the client has to interpret.
/// </param>
public sealed record EligibilityResponse(bool IsEligible, IReadOnlyList<EligibleMarketResponse> Markets, string Message);
