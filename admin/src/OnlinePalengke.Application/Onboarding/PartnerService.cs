using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Onboarding;

/// <summary>Partner profile registration, self-service lookup, and status changes.</summary>
/// <remarks>
/// <see cref="Partner"/> is deliberately plain data with no domain methods (matching
/// Category/Item/Market's convention, not MediaAsset's guarded-mutation one) — every
/// status change here is a full-row reconstruction followed by a repository update, not a
/// mutation on the entity itself.
/// </remarks>
public sealed class PartnerService(
    IPartnerRepository partners,
    ICategoryRepository categories,
    IMarketRepository markets,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<PartnerResponse> RegisterAsync(
        RegisterPartnerRequest request, CancellationToken cancellationToken = default)
    {
        if (await partners.GetByUserIdAsync(currentUser.UserId, cancellationToken) is not null)
        {
            throw new ConflictException("This account is already registered as a partner.");
        }

        var stallName = RequireStallName(request.StallName);

        if (request.CategoryId is { } categoryId
            && await categories.GetByIdAsync(categoryId, cancellationToken) is null)
        {
            throw new ValidationException(nameof(request.CategoryId), "That category does not exist.");
        }

        if (request.MarketId is { } marketId
            && await markets.GetByIdAsync(marketId, cancellationToken) is null)
        {
            throw new ValidationException(nameof(request.MarketId), "That market does not exist.");
        }

        var now = clock.UtcNow;
        var partner = new Partner
        {
            UserId = currentUser.UserId,
            StallName = stallName,
            MarketId = request.MarketId,
            CategoryId = request.CategoryId,
            Status = PartnerStatus.PendingKyc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await partners.InsertAsync(partner, cancellationToken);
        return new PartnerResponse(
            id, partner.UserId, partner.StallName, partner.MarketId, partner.CategoryId,
            Naming.ToDbValue(partner.Status), partner.CreatedAtUtc, partner.UpdatedAtUtc);
    }

    public async Task<PartnerResponse> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        var partner = await partners.GetByUserIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("No partner profile exists for this account yet.");

        return ToResponse(partner);
    }

    public async Task<PartnerResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var partner = await partners.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Partner {id} was not found.");

        return ToResponse(partner);
    }

    public async Task<IReadOnlyList<PartnerResponse>> ListAsync(
        PartnerStatus? status = null, CancellationToken cancellationToken = default)
    {
        var rows = await partners.ListAsync(status, cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    /// <summary>Only promotes when the partner is currently <see cref="PartnerStatus.PendingKyc"/> — a no-op otherwise.</summary>
    /// <remarks>
    /// Called by <c>KycDocumentService</c> on a partner's first document submission. A
    /// later submission (e.g. resubmitting one lapsed document while otherwise verified)
    /// must not bounce an already-verified partner back to under-review — only the
    /// verified-derivation re-check after an approval changes status from that point on.
    /// </remarks>
    public async Task MarkUnderReviewIfPendingAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await partners.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Partner {id} was not found.");

        if (existing.Status == PartnerStatus.PendingKyc)
        {
            await ChangeStatusAsync(existing, PartnerStatus.UnderReview, cancellationToken);
        }
    }

    public async Task<Partner> MarkVerifiedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await partners.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Partner {id} was not found.");

        return await ChangeStatusAsync(existing, PartnerStatus.Verified, cancellationToken);
    }

    public async Task<Partner> MarkSuspendedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await partners.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Partner {id} was not found.");

        return await ChangeStatusAsync(existing, PartnerStatus.Suspended, cancellationToken);
    }

    public async Task<Partner> MarkRejectedAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await partners.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Partner {id} was not found.");

        return await ChangeStatusAsync(existing, PartnerStatus.Rejected, cancellationToken);
    }

    private async Task<Partner> ChangeStatusAsync(
        Partner existing, PartnerStatus newStatus, CancellationToken cancellationToken)
    {
        var updated = new Partner
        {
            Id = existing.Id,
            UserId = existing.UserId,
            StallName = existing.StallName,
            MarketId = existing.MarketId,
            CategoryId = existing.CategoryId,
            Status = newStatus,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await partners.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Partner {existing.Id} was not found.");
        }

        return updated;
    }

    private static PartnerResponse ToResponse(Partner partner) => new(
        partner.Id,
        partner.UserId,
        partner.StallName,
        partner.MarketId,
        partner.CategoryId,
        Naming.ToDbValue(partner.Status),
        partner.CreatedAtUtc,
        partner.UpdatedAtUtc);

    private static string RequireStallName(string? stallName)
    {
        if (string.IsNullOrWhiteSpace(stallName))
        {
            throw new ValidationException(nameof(stallName), "A stall name is required.");
        }

        return stallName.Trim();
    }
}
