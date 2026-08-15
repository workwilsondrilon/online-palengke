namespace OnlinePalengke.Domain.Media;

/// <summary>
/// What an uploaded file is for. The purpose — not the caller — decides which bucket
/// the object lands in, how large it may be, and which content types are acceptable.
/// </summary>
/// <remarks>
/// Persisted as snake_case strings in <c>media_assets.purpose</c>.
/// </remarks>
public enum MediaPurpose
{
    /// <summary>
    /// A partner's or rider's identity, permit or clearance document. Always private —
    /// these are government IDs and business permits and must never be publicly readable.
    /// </summary>
    KycDocument,

    /// <summary>A stall's marketing photo for one product. Publicly readable; renders on quote cards.</summary>
    PartnerProduct,

    /// <summary>An admin-curated item master image. Publicly readable.</summary>
    ItemImage,

    /// <summary>
    /// A rider's proof-of-delivery photo. Private — it shows the inside of a customer's
    /// doorway and is evidence in a dispute, not public content.
    /// </summary>
    DeliveryProof,

    /// <summary>A user's avatar. Publicly readable.</summary>
    ProfilePhoto,
}

/// <summary>
/// Lifecycle of an uploaded object. An asset is created <see cref="Pending"/> when the
/// presigned URL is issued and only becomes <see cref="Committed"/> once the client
/// confirms the upload landed.
/// </summary>
/// <remarks>
/// A daily sweep deletes <see cref="Pending"/> assets older than 24 hours, because a
/// client that crashes between presign and PUT would otherwise leak a row forever.
/// </remarks>
public enum MediaAssetState
{
    Pending,
    Committed,
}
