using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Storefront;

/// <summary>
/// A partner's product declarations — the marketing card that renders on a quote and on
/// the stall's read-only profile (decision 18). Publishes immediately on save, with no
/// pre-approval; moderation is reactive, via <see cref="Moderation.ContentModerationService"/>.
/// </summary>
public sealed class PartnerProductService(
    IPartnerProductRepository products,
    IItemRepository items,
    IMediaAssetRepository mediaAssets,
    IPartnerRepository partners,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<PartnerProductResponse> DeclareAsync(
        DeclareProductRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await ResolveCallingPartnerAsync(cancellationToken);

        if (await items.GetByIdAsync(request.ItemId, cancellationToken) is null)
        {
            throw new ValidationException(nameof(request.ItemId), "That item does not exist.");
        }

        if (await products.GetByPartnerAndItemAsync(partner.Id, request.ItemId, cancellationToken) is not null)
        {
            throw new ConflictException("You have already declared this item. Edit the existing entry instead.");
        }

        var headline = RequireHeadline(request.Headline);
        await ValidateMediaAssetAsync(request.MediaAssetId, cancellationToken);

        var now = clock.UtcNow;
        var product = new PartnerProduct
        {
            PartnerId = partner.Id,
            ItemId = request.ItemId,
            MediaAssetId = request.MediaAssetId,
            Headline = headline,
            Description = NormalizeDescription(request.Description),
            IsPublished = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await products.InsertAsync(product, cancellationToken);
        return ToResponse(product, id);
    }

    public async Task<PartnerProductResponse> UpdateAsync(
        long id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await ResolveCallingPartnerAsync(cancellationToken);
        var existing = await products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

        if (existing.PartnerId != partner.Id)
        {
            throw new ForbiddenException("This product does not belong to you.");
        }

        var headline = RequireHeadline(request.Headline);
        await ValidateMediaAssetAsync(request.MediaAssetId, cancellationToken);

        var updated = new PartnerProduct
        {
            Id = existing.Id,
            PartnerId = existing.PartnerId,
            ItemId = existing.ItemId,
            MediaAssetId = request.MediaAssetId,
            Headline = headline,
            Description = NormalizeDescription(request.Description),
            IsPublished = existing.IsPublished,
            UnpublishedBy = existing.UnpublishedBy,
            UnpublishedReason = existing.UnpublishedReason,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await products.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Product {id} was not found.");
        }

        return ToResponse(updated, id);
    }

    /// <summary>Re-publishes a partner's own product, e.g. after fixing whatever an admin took it down for.</summary>
    public async Task<PartnerProductResponse> PublishAsync(long id, CancellationToken cancellationToken = default)
    {
        var partner = await ResolveCallingPartnerAsync(cancellationToken);
        var existing = await products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

        if (existing.PartnerId != partner.Id)
        {
            throw new ForbiddenException("This product does not belong to you.");
        }

        return await SetPublishedAsync(existing, isPublished: true, by: null, reason: null, cancellationToken);
    }

    /// <summary>
    /// Unpublishes a product. Called by the owning partner directly, or by an admin acting
    /// on a content report (<see cref="Moderation.ContentModerationService"/>) — either
    /// caller passes its own user id as <paramref name="by"/>.
    /// </summary>
    public async Task<PartnerProductResponse> UnpublishAsync(
        long id, long by, string? reason, CancellationToken cancellationToken = default)
    {
        var existing = await products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

        return await SetPublishedAsync(existing, isPublished: false, by, reason, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerProductResponse>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var partner = await ResolveCallingPartnerAsync(cancellationToken);
        var rows = await products.ListForPartnerAsync(partner.Id, cancellationToken);
        return rows.Select(p => ToResponse(p, p.Id)).ToList();
    }

    public async Task<IReadOnlyList<PartnerProductResponse>> ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        var rows = await products.ListPublishedAsync(cancellationToken);
        return rows.Select(p => ToResponse(p, p.Id)).ToList();
    }

    public async Task<PartnerProductResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

        return ToResponse(product, id);
    }

    private async Task<PartnerProductResponse> SetPublishedAsync(
        PartnerProduct existing, bool isPublished, long? by, string? reason, CancellationToken cancellationToken)
    {
        var updated = new PartnerProduct
        {
            Id = existing.Id,
            PartnerId = existing.PartnerId,
            ItemId = existing.ItemId,
            MediaAssetId = existing.MediaAssetId,
            Headline = existing.Headline,
            Description = existing.Description,
            IsPublished = isPublished,
            UnpublishedBy = isPublished ? null : by,
            UnpublishedReason = isPublished ? null : reason,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await products.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Product {existing.Id} was not found.");
        }

        return ToResponse(updated, existing.Id);
    }

    private async Task<Domain.Identity.Partner> ResolveCallingPartnerAsync(CancellationToken cancellationToken) =>
        await partners.GetByUserIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Register as a partner before declaring products.");

    private async Task ValidateMediaAssetAsync(long? mediaAssetId, CancellationToken cancellationToken)
    {
        if (mediaAssetId is null)
        {
            return;
        }

        var asset = await mediaAssets.GetByIdAsync(mediaAssetId.Value, cancellationToken)
            ?? throw new ValidationException(nameof(mediaAssetId), "That upload was not found.");

        if (asset.UploadedBy != currentUser.UserId)
        {
            throw new ForbiddenException("That upload does not belong to you.");
        }

        if (asset.State != MediaAssetState.Committed)
        {
            throw new ValidationException(nameof(mediaAssetId), "Finish uploading the file first.");
        }

        if (asset.Purpose != MediaPurpose.PartnerProduct)
        {
            throw new ValidationException(nameof(mediaAssetId), "That upload was not made for a product photo.");
        }
    }

    private static PartnerProductResponse ToResponse(PartnerProduct product, long id) => new(
        id,
        product.PartnerId,
        product.ItemId,
        product.MediaAssetId,
        product.Headline,
        product.Description,
        product.IsPublished,
        product.UnpublishedBy,
        product.UnpublishedReason,
        product.CreatedAtUtc,
        product.UpdatedAtUtc);

    private static string RequireHeadline(string? headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            throw new ValidationException(nameof(headline), "A headline is required.");
        }

        return headline.Trim();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
