using OnlinePalengke.Domain.Common;

namespace OnlinePalengke.Domain.Media;

/// <summary>
/// A file in object storage, tracked so that orphans can be swept and so that every
/// stored object has a known owner and purpose.
/// </summary>
/// <remarks>
/// The row is written when the presigned URL is issued, before any bytes exist in S3.
/// That ordering is deliberate: it means an object can never appear in a bucket without
/// a corresponding record explaining who put it there and why.
/// </remarks>
public sealed class MediaAsset
{
    public long Id { get; init; }

    /// <summary>The bucket the object lives in, resolved from the purpose's visibility.</summary>
    public required string Bucket { get; init; }

    /// <summary>
    /// The object key. Always server-generated — a client-supplied key is a path-traversal
    /// and overwrite hazard, so the presign endpoint never accepts one.
    /// </summary>
    public required string ObjectKey { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public required MediaPurpose Purpose { get; init; }

    /// <summary>The user who requested the presigned URL.</summary>
    public required long UploadedBy { get; init; }

    public MediaAssetState State { get; private set; } = MediaAssetState.Pending;

    public required DateTime CreatedAt { get; init; }

    public DateTime? CommittedAt { get; private set; }

    /// <summary>
    /// Marks the asset as confirmed present in the bucket. Idempotent by design: a client
    /// on a flaky connection may well send the commit twice, and that should not be an error.
    /// </summary>
    public void Commit(DateTime committedAtUtc)
    {
        if (State == MediaAssetState.Committed)
        {
            return;
        }

        State = MediaAssetState.Committed;
        CommittedAt = committedAtUtc;
    }

    /// <summary>
    /// Builds the storage key for a new upload. Partitioned by purpose and owner so that
    /// a bucket policy or IAM statement can be scoped to a prefix, and by year/month so
    /// that no single prefix accumulates unbounded objects.
    /// </summary>
    public static string BuildObjectKey(MediaPurpose purpose, long ownerUserId, string contentType, DateTime nowUtc)
    {
        var folder = Naming.ToSnakeCase(purpose.ToString());
        var extension = ExtensionFor(contentType);

        return $"{folder}/{ownerUserId}/{nowUtc:yyyy}/{nowUtc:MM}/{Guid.NewGuid():N}{extension}";
    }

    private static string ExtensionFor(string contentType) => contentType.Trim().ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "application/pdf" => ".pdf",
        _ => string.Empty,
    };
}
