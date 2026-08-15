using System.ComponentModel.DataAnnotations;

namespace OnlinePalengke.Infrastructure.Storage;

/// <summary>Object storage configuration, bound from the <c>Storage</c> configuration section.</summary>
/// <remarks>
/// Credentials are deliberately absent. The SDK resolves them from the standard AWS
/// chain — environment variables and the shared profile locally, an IAM role when
/// deployed. Access keys must never appear in configuration files or in this repo.
/// </remarks>
public sealed class S3Options
{
    public const string SectionName = "Storage";

    /// <summary>AWS region, e.g. <c>ap-southeast-1</c> (Singapore, nearest to the Philippines).</summary>
    [Required(AllowEmptyStrings = false)]
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Public-read bucket: item master images, partner marketing photos, profile photos.
    /// Objects here are served straight from their URL with no signing.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PublicBucket { get; init; } = string.Empty;

    /// <summary>
    /// Private bucket with Block Public Access enabled: KYC documents and delivery proof.
    /// Readable only through a short-TTL presigned GET.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PrivateBucket { get; init; } = string.Empty;

    /// <summary>
    /// Optional origin for public objects, e.g. a CDN domain. When empty, the plain
    /// regional S3 URL is used. Set this once a CDN sits in front of the public bucket —
    /// quote cards load several images at once and S3 egress adds up.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>How long a presigned GET for a private object stays valid.</summary>
    [Range(1, 60)]
    public int PrivateReadUrlMinutes { get; init; } = 5;
}
