using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Catalog;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Application.Markets;

/// <summary>Admin CRUD for markets, plus saving the delivery service area drawn on the map.</summary>
public sealed class MarketService(IMarketRepository markets, IClock clock)
{
    public async Task<IReadOnlyList<MarketResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await markets.ListAsync(cancellationToken);
        return rows.Select(m => ToResponse(m)).ToList();
    }

    public async Task<MarketResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var market = await markets.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Market {id} was not found.");

        return ToResponse(market);
    }

    public async Task<MarketResponse> CreateAsync(CreateMarketRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireNonEmpty(request.Name, nameof(request.Name), "A market name is required.");
        var address = RequireNonEmpty(request.Address, nameof(request.Address), "An address is required.");
        var city = RequireNonEmpty(request.City, nameof(request.City), "A city is required.");
        var province = RequireNonEmpty(request.Province, nameof(request.Province), "A province is required.");
        RequireValidLatLng(request.Lat, request.Lng);

        var now = clock.UtcNow;
        var market = new Market
        {
            Name = name,
            Address = address,
            City = city,
            Province = province,
            Lat = request.Lat,
            Lng = request.Lng,
            ServiceAreaWkt = null,
            // Every market starts Onboarding: there is nowhere to draw a service area
            // before the market row exists, so it cannot go live in the same request that
            // creates it (decision recorded in Market.cs's XML remarks).
            Status = MarketStatus.Onboarding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await markets.InsertAsync(market, cancellationToken);
        return ToResponse(market, id);
    }

    public async Task<MarketResponse> UpdateAsync(
        long id,
        UpdateMarketRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await markets.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Market {id} was not found.");

        var name = RequireNonEmpty(request.Name, nameof(request.Name), "A market name is required.");
        var address = RequireNonEmpty(request.Address, nameof(request.Address), "An address is required.");
        var city = RequireNonEmpty(request.City, nameof(request.City), "A city is required.");
        var province = RequireNonEmpty(request.Province, nameof(request.Province), "A province is required.");
        RequireValidLatLng(request.Lat, request.Lng);
        var status = ParseStatus(request.Status);

        if (status == MarketStatus.Active && !existing.HasServiceArea)
        {
            throw new ValidationException(
                nameof(request.Status),
                "Draw and save a delivery service area before activating this market.");
        }

        var updated = new Market
        {
            Id = id,
            Name = name,
            Address = address,
            City = city,
            Province = province,
            Lat = request.Lat,
            Lng = request.Lng,
            ServiceAreaWkt = existing.ServiceAreaWkt,
            Status = status,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await markets.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Market {id} was not found.");
        }

        return ToResponse(updated);
    }

    /// <summary>
    /// Saves (or clears) the delivery polygon drawn on the market's map panel.
    /// </summary>
    /// <remarks>
    /// This is the one place a GeoJSON string from Leaflet.draw turns into the WKT stored
    /// in <see cref="Market.ServiceAreaWkt"/> — see <see cref="PolygonWkt"/> for the
    /// latitude/longitude axis flip this conversion has to get right.
    /// </remarks>
    public async Task<MarketResponse> UpdateServiceAreaAsync(
        long id,
        UpdateServiceAreaRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await markets.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Market {id} was not found.");

        string? wkt = null;
        if (!string.IsNullOrWhiteSpace(request.PolygonGeoJson))
        {
            try
            {
                wkt = PolygonWkt.FromGeoJson(request.PolygonGeoJson);
            }
            catch (FormatException ex)
            {
                throw new ValidationException(nameof(request.PolygonGeoJson), ex.Message);
            }
        }

        var updatedAt = clock.UtcNow;
        if (!await markets.UpdateServiceAreaAsync(id, wkt, updatedAt, cancellationToken))
        {
            throw new NotFoundException($"Market {id} was not found.");
        }

        var updated = new Market
        {
            Id = existing.Id,
            Name = existing.Name,
            Address = existing.Address,
            City = existing.City,
            Province = existing.Province,
            Lat = existing.Lat,
            Lng = existing.Lng,
            ServiceAreaWkt = wkt,
            Status = existing.Status,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = updatedAt,
        };

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await markets.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Market {id} was not found.");

        try
        {
            if (!await markets.DeleteAsync(id, cancellationToken))
            {
                throw new NotFoundException($"Market {id} was not found.");
            }
        }
        catch (Exception ex) when (CategoryService.IsForeignKeyViolation(ex))
        {
            throw new ConflictException(
                "This market still has partners, delivery windows or other records attached to it.");
        }
    }

    private static MarketResponse ToResponse(Market market, long? overrideId = null) => new(
        overrideId ?? market.Id,
        market.Name,
        market.Address,
        market.City,
        market.Province,
        market.Lat,
        market.Lng,
        market.ServiceAreaWkt is null ? null : PolygonWkt.ToGeoJson(market.ServiceAreaWkt),
        market.HasServiceArea,
        Naming.ToDbValue(market.Status),
        market.CreatedAtUtc,
        market.UpdatedAtUtc);

    private static string RequireNonEmpty(string? value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(field, message);
        }

        return value.Trim();
    }

    private static void RequireValidLatLng(decimal lat, decimal lng)
    {
        if (lat is < -90 or > 90)
        {
            throw new ValidationException("lat", "Latitude must be between -90 and 90.");
        }

        if (lng is < -180 or > 180)
        {
            throw new ValidationException("lng", "Longitude must be between -180 and 180.");
        }
    }

    private static MarketStatus ParseStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("status", "A status is required.");
        }

        foreach (var candidate in Enum.GetValues<MarketStatus>())
        {
            if (string.Equals(Naming.ToDbValue(candidate), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        var known = string.Join(", ", Enum.GetValues<MarketStatus>().Select(Naming.ToDbValue));
        throw new ValidationException("status", $"Unknown status '{value}'. Expected one of: {known}.");
    }
}
