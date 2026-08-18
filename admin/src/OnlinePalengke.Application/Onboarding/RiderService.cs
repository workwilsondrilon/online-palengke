using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Onboarding;

/// <summary>Rider profile registration, self-service lookup, and status changes.</summary>
/// <remarks>Mirrors <see cref="PartnerService"/> exactly — see its remarks for why status changes are full-row rebuilds.</remarks>
public sealed class RiderService(IRiderRepository riders, ICurrentUser currentUser, IClock clock)
{
    public async Task<RiderResponse> RegisterAsync(
        RegisterRiderRequest request, CancellationToken cancellationToken = default)
    {
        if (await riders.GetByUserIdAsync(currentUser.UserId, cancellationToken) is not null)
        {
            throw new ConflictException("This account is already registered as a rider.");
        }

        var fullName = RequireFullName(request.FullName);
        var now = clock.UtcNow;
        var rider = new Rider
        {
            UserId = currentUser.UserId,
            FullName = fullName,
            VehicleType = string.IsNullOrWhiteSpace(request.VehicleType) ? null : request.VehicleType.Trim(),
            Status = RiderStatus.PendingKyc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await riders.InsertAsync(rider, cancellationToken);
        return new RiderResponse(
            id, rider.UserId, rider.FullName, rider.VehicleType,
            Naming.ToDbValue(rider.Status), rider.CreatedAtUtc, rider.UpdatedAtUtc);
    }

    public async Task<RiderResponse> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        var rider = await riders.GetByUserIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("No rider profile exists for this account yet.");

        return ToResponse(rider);
    }

    public async Task<RiderResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var rider = await riders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Rider {id} was not found.");

        return ToResponse(rider);
    }

    public async Task<IReadOnlyList<RiderResponse>> ListAsync(
        RiderStatus? status = null, CancellationToken cancellationToken = default)
    {
        var rows = await riders.ListAsync(status, cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    /// <summary>Only promotes when the rider is currently <see cref="RiderStatus.PendingKyc"/> — a no-op otherwise. See <see cref="PartnerService.MarkUnderReviewIfPendingAsync"/>.</summary>
    public async Task MarkUnderReviewIfPendingAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await riders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Rider {id} was not found.");

        if (existing.Status == RiderStatus.PendingKyc)
        {
            await ChangeStatusAsync(existing, RiderStatus.UnderReview, cancellationToken);
        }
    }

    public async Task<Rider> MarkVerifiedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await riders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Rider {id} was not found.");

        return await ChangeStatusAsync(existing, RiderStatus.Verified, cancellationToken);
    }

    public async Task<Rider> MarkSuspendedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await riders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Rider {id} was not found.");

        return await ChangeStatusAsync(existing, RiderStatus.Suspended, cancellationToken);
    }

    public async Task<Rider> MarkRejectedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await riders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Rider {id} was not found.");

        return await ChangeStatusAsync(existing, RiderStatus.Rejected, cancellationToken);
    }

    private async Task<Rider> ChangeStatusAsync(
        Rider existing, RiderStatus newStatus, CancellationToken cancellationToken)
    {
        var updated = new Rider
        {
            Id = existing.Id,
            UserId = existing.UserId,
            FullName = existing.FullName,
            VehicleType = existing.VehicleType,
            Status = newStatus,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await riders.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Rider {existing.Id} was not found.");
        }

        return updated;
    }

    private static RiderResponse ToResponse(Rider rider) => new(
        rider.Id,
        rider.UserId,
        rider.FullName,
        rider.VehicleType,
        Naming.ToDbValue(rider.Status),
        rider.CreatedAtUtc,
        rider.UpdatedAtUtc);

    private static string RequireFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ValidationException(nameof(fullName), "A full name is required.");
        }

        return fullName.Trim();
    }
}
