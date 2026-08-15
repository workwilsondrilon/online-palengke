namespace OnlinePalengke.Admin.Api;

/// <summary>Wire DTOs for <c>/api/admin/markets</c> and its delivery-window sub-resource.</summary>
/// <remarks>Mirrors <c>OnlinePalengke.Application.Markets.MarketContracts</c> - see <see cref="CategoryDto"/>'s remarks.</remarks>
public sealed record MarketDto(
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

public sealed record UpdateMarketRequest(
    string Name,
    string Address,
    string City,
    string Province,
    decimal Lat,
    decimal Lng,
    string Status);

public sealed record UpdateServiceAreaRequest(string? PolygonGeoJson);

public sealed record DeliveryWindowDto(
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
