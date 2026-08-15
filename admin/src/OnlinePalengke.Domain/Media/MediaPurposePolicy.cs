using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Domain.Media;

/// <summary>
/// The rules governing every upload: who may request one, where it lands, how big it
/// may be, and what it may contain.
/// </summary>
/// <remarks>
/// This is deliberately domain logic rather than configuration. These are security
/// boundaries — a customer must never be able to write into the KYC bucket, and a
/// partner must never be able to make a government ID publicly readable. Putting the
/// table in code means the rules are unit-testable and cannot be loosened by editing
/// an appsettings file in production.
/// </remarks>
public static class MediaPurposePolicy
{
    private const long OneMegabyte = 1024L * 1024L;

    private static readonly string[] ImageTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private static readonly string[] DocumentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private static readonly Dictionary<MediaPurpose, MediaPurposeRule> Rules = new()
    {
        // Permits are often photographed badly or scanned as multi-page PDFs, so this
        // allows more headroom than the image purposes and accepts PDF.
        [MediaPurpose.KycDocument] = new MediaPurposeRule(
            Visibility: MediaVisibility.Private,
            MaxSizeBytes: 10 * OneMegabyte,
            AllowedContentTypes: DocumentTypes,
            AllowedRoles: [UserRole.Partner, UserRole.Rider]),

        // Clients compress to roughly 2 MB before upload; 4 MB is the server-side
        // backstop for a client that does not, not the expected size.
        [MediaPurpose.PartnerProduct] = new MediaPurposeRule(
            Visibility: MediaVisibility.Public,
            MaxSizeBytes: 4 * OneMegabyte,
            AllowedContentTypes: ImageTypes,
            AllowedRoles: [UserRole.Partner]),

        [MediaPurpose.ItemImage] = new MediaPurposeRule(
            Visibility: MediaVisibility.Public,
            MaxSizeBytes: 4 * OneMegabyte,
            AllowedContentTypes: ImageTypes,
            AllowedRoles: [UserRole.Admin]),

        [MediaPurpose.DeliveryProof] = new MediaPurposeRule(
            Visibility: MediaVisibility.Private,
            MaxSizeBytes: 4 * OneMegabyte,
            AllowedContentTypes: ImageTypes,
            AllowedRoles: [UserRole.Rider]),

        [MediaPurpose.ProfilePhoto] = new MediaPurposeRule(
            Visibility: MediaVisibility.Public,
            MaxSizeBytes: 2 * OneMegabyte,
            AllowedContentTypes: ImageTypes,
            AllowedRoles: [UserRole.Customer, UserRole.Partner, UserRole.Rider, UserRole.Admin]),
    };

    /// <summary>Every purpose that has a rule. Used by tests to assert the table is exhaustive.</summary>
    public static IReadOnlyCollection<MediaPurpose> AllPurposes => Rules.Keys;

    /// <summary>Looks up the rule for a purpose. Every declared purpose has one.</summary>
    public static MediaPurposeRule For(MediaPurpose purpose) =>
        Rules.TryGetValue(purpose, out var rule)
            ? rule
            : throw new ArgumentOutOfRangeException(
                nameof(purpose), purpose, "No upload rule is defined for this purpose.");

    /// <summary>
    /// Validates a presign request against the policy. Returns the reason for refusal,
    /// or <c>null</c> if the request is acceptable.
    /// </summary>
    public static string? Validate(MediaPurpose purpose, UserRole role, string contentType, long sizeBytes)
    {
        var rule = For(purpose);

        if (!rule.AllowedRoles.Contains(role))
        {
            return $"A {Naming.ToSnakeCase(role.ToString())} may not upload for the " +
                   $"'{Naming.ToSnakeCase(purpose.ToString())}' purpose.";
        }

        if (sizeBytes <= 0)
        {
            return "Declared size must be greater than zero.";
        }

        if (sizeBytes > rule.MaxSizeBytes)
        {
            return $"File is {sizeBytes / OneMegabyte:F1} MB, which exceeds the " +
                   $"{rule.MaxSizeBytes / OneMegabyte} MB limit for this purpose.";
        }

        var normalised = contentType.Trim().ToLowerInvariant();
        if (!rule.AllowedContentTypes.Contains(normalised))
        {
            return $"Content type '{contentType}' is not accepted for this purpose. " +
                   $"Allowed: {string.Join(", ", rule.AllowedContentTypes)}.";
        }

        return null;
    }
}

/// <summary>Which bucket an object belongs in, and therefore how it is read back.</summary>
public enum MediaVisibility
{
    /// <summary>Public-read bucket. Served straight from its URL — no signing on read.</summary>
    Public,

    /// <summary>Private bucket with Block Public Access on. Readable only via a short-TTL presigned GET.</summary>
    Private,
}

/// <summary>The upload constraints for a single <see cref="MediaPurpose"/>.</summary>
/// <param name="Visibility">Which bucket the object lands in.</param>
/// <param name="MaxSizeBytes">Server-side ceiling on the declared size.</param>
/// <param name="AllowedContentTypes">Exact, lowercase MIME types. Not a prefix match.</param>
/// <param name="AllowedRoles">Roles permitted to request a presign for this purpose.</param>
public sealed record MediaPurposeRule(
    MediaVisibility Visibility,
    long MaxSizeBytes,
    IReadOnlyList<string> AllowedContentTypes,
    IReadOnlyList<UserRole> AllowedRoles);
