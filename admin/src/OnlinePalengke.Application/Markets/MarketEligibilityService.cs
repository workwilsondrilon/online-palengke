using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;

namespace OnlinePalengke.Application.Markets;

/// <summary>
/// The customer-facing half of decision 14: given a candidate delivery point, which
/// markets — if any — will actually deliver there.
/// </summary>
/// <remarks>
/// Deliberately returns every matching market rather than just a yes/no: an address can
/// legitimately sit inside more than one market's service area (two markets with
/// overlapping polygons), and the customer app needs the full list to let a customer pick
/// which one to shop.
/// </remarks>
public sealed class MarketEligibilityService(IMarketRepository markets)
{
    public async Task<EligibilityResponse> CheckAsync(
        EligibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lat is < -90 or > 90)
        {
            throw new ValidationException("lat", "Latitude must be between -90 and 90.");
        }

        if (request.Lng is < -180 or > 180)
        {
            throw new ValidationException("lng", "Longitude must be between -180 and 180.");
        }

        var matches = await markets.FindActiveContainingPointAsync(request.Lat, request.Lng, cancellationToken);

        if (matches.Count == 0)
        {
            return new EligibilityResponse(
                IsEligible: false,
                Markets: [],
                Message: "Sorry, this address is outside our current delivery area.");
        }

        var eligible = matches
            .Select(m => new EligibleMarketResponse(m.Id, m.Name, m.City, m.Province))
            .ToList();

        return new EligibilityResponse(
            IsEligible: true,
            Markets: eligible,
            Message: eligible.Count == 1
                ? $"Deliverable from {eligible[0].Name}."
                : $"Deliverable from {eligible.Count} markets in this area.");
    }
}
