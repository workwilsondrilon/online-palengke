namespace OnlinePalengke.Domain.Media;

/// <summary>
/// Declares that a partner carries an item, with optional marketing (image, headline,
/// description) that renders on the item's quote card and the partner's read-only stall
/// profile (Epic 5) once published.
/// </summary>
/// <remarks>
/// The row's existence — independent of <see cref="IsPublished"/> — is what drives Epic
/// 5's quote-request matching: "this stall carries this item" is true whether or not its
/// marketing is currently visible. Publish-on-save, no pre-approval gate (decision 19);
/// <see cref="Unpublish"/> is used both for the partner's own take-down and for an admin
/// moderation take-down (<see cref="UnpublishedBy"/> distinguishes which), and never
/// suspends the partner or deletes the row.
/// </remarks>
public sealed class PartnerProduct
{
    public long Id { get; init; }

    public required long PartnerId { get; init; }

    public required long ItemId { get; init; }

    public long? MediaAssetId { get; init; }

    public required string Headline { get; init; }

    public string? Description { get; init; }

    public required bool IsPublished { get; init; }

    /// <summary>The user (partner or admin) who last unpublished this product, if any.</summary>
    public long? UnpublishedBy { get; init; }

    public string? UnpublishedReason { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
