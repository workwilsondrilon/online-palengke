namespace OnlinePalengke.Application.Storefront;

public sealed record DeclareProductRequest(long ItemId, long? MediaAssetId, string Headline, string? Description);

public sealed record UpdateProductRequest(long? MediaAssetId, string Headline, string? Description);

public sealed record PartnerProductResponse(
    long Id,
    long PartnerId,
    long ItemId,
    long? MediaAssetId,
    string Headline,
    string? Description,
    bool IsPublished,
    long? UnpublishedBy,
    string? UnpublishedReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
