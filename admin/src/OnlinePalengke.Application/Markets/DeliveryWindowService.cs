using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.Application.Markets;

/// <summary>Admin CRUD for a market's recurring delivery windows.</summary>
public sealed class DeliveryWindowService(
    IDeliveryWindowRepository windows,
    IMarketRepository markets,
    IClock clock)
{
    public async Task<IReadOnlyList<DeliveryWindowResponse>> ListForMarketAsync(
        long marketId,
        CancellationToken cancellationToken = default)
    {
        var rows = await windows.ListForMarketAsync(marketId, cancellationToken);
        return rows.Select(w => ToResponse(w)).ToList();
    }

    public async Task<DeliveryWindowResponse> CreateAsync(
        long marketId,
        CreateDeliveryWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await markets.GetByIdAsync(marketId, cancellationToken)
            ?? throw new NotFoundException($"Market {marketId} was not found.");

        var label = RequireLabel(request.Label);
        RequireValidTimes(request.StartsAt, request.EndsAt);
        RequireNonNegative(request.CutoffOffsetMinutes, "cutoffOffsetMinutes", "Cutoff offset cannot be negative.");
        RequirePositive(request.Capacity, "capacity", "Capacity must be greater than zero.");

        var now = clock.UtcNow;
        var window = new DeliveryWindow
        {
            MarketId = marketId,
            Label = label,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            CutoffOffsetMinutes = request.CutoffOffsetMinutes,
            Capacity = request.Capacity,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await windows.InsertAsync(window, cancellationToken);
        return ToResponse(window, id);
    }

    public async Task<DeliveryWindowResponse> UpdateAsync(
        long id,
        UpdateDeliveryWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await windows.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Delivery window {id} was not found.");

        var label = RequireLabel(request.Label);
        RequireValidTimes(request.StartsAt, request.EndsAt);
        RequireNonNegative(request.CutoffOffsetMinutes, "cutoffOffsetMinutes", "Cutoff offset cannot be negative.");
        RequirePositive(request.Capacity, "capacity", "Capacity must be greater than zero.");

        var updated = new DeliveryWindow
        {
            Id = id,
            MarketId = existing.MarketId,
            Label = label,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            CutoffOffsetMinutes = request.CutoffOffsetMinutes,
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await windows.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Delivery window {id} was not found.");
        }

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await windows.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Delivery window {id} was not found.");

        if (!await windows.DeleteAsync(id, cancellationToken))
        {
            throw new NotFoundException($"Delivery window {id} was not found.");
        }
    }

    private static DeliveryWindowResponse ToResponse(DeliveryWindow window, long? overrideId = null) => new(
        overrideId ?? window.Id,
        window.MarketId,
        window.Label,
        window.StartsAt,
        window.EndsAt,
        window.CutoffOffsetMinutes,
        window.Capacity,
        window.IsActive);

    private static string RequireLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ValidationException("label", "A label is required, e.g. '7-9 AM'.");
        }

        return label.Trim();
    }

    private static void RequireValidTimes(TimeOnly startsAt, TimeOnly endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new ValidationException("endsAt", "The window must end after it starts, and cannot cross midnight.");
        }
    }

    private static void RequireNonNegative(int value, string field, string message)
    {
        if (value < 0)
        {
            throw new ValidationException(field, message);
        }
    }

    private static void RequirePositive(int value, string field, string message)
    {
        if (value <= 0)
        {
            throw new ValidationException(field, message);
        }
    }
}
