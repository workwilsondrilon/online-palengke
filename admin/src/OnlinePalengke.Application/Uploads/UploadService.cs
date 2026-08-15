using Microsoft.Extensions.Logging;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Uploads;

/// <summary>
/// Issues presigned upload URLs and confirms the resulting objects.
/// </summary>
/// <remarks>
/// Three rules hold this together, and none of them may be relaxed:
/// <list type="number">
/// <item>The object key is generated here. A client never supplies one, because a
/// client-chosen key can traverse prefixes and overwrite someone else's object.</item>
/// <item>The bucket comes from the purpose's visibility, never from the request. That is
/// what stops a government ID from landing in the public-read bucket.</item>
/// <item>Commit verifies the object actually exists in S3 before marking it committed,
/// so a caller cannot attach a phantom asset to a KYC submission.</item>
/// </list>
/// </remarks>
public sealed class UploadService(
    IMediaAssetRepository mediaAssets,
    IFileStorage storage,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<UploadService> logger)
{
    /// <summary>
    /// How long a presigned PUT stays valid. Long enough for a large permit scan on a
    /// slow mobile connection, short enough that a leaked URL is not a durable hole.
    /// </summary>
    private static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromMinutes(15);

    public async Task<PresignUploadResponse> PresignAsync(
        PresignUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var purpose = ParsePurpose(request.Purpose);

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ValidationException(nameof(request.ContentType), "A content type is required.");
        }

        var role = currentUser.Role;
        var refusal = MediaPurposePolicy.Validate(purpose, role, request.ContentType, request.SizeBytes);
        if (refusal is not null)
        {
            // Logged at warning rather than information: a client asking for something the
            // policy forbids is either a bug in that client or someone probing the boundary.
            logger.LogWarning(
                "Refused presign for user {UserId} ({Role}) purpose {Purpose}: {Reason}",
                currentUser.UserId, role, purpose, refusal);

            // A role that is not permitted this purpose at all is an authorization failure,
            // not a malformed request, and should read as one to the caller.
            throw MediaPurposePolicy.For(purpose).AllowedRoles.Contains(role)
                ? new ValidationException(refusal)
                : new ForbiddenException(refusal);
        }

        var rule = MediaPurposePolicy.For(purpose);
        var bucket = storage.ResolveBucket(rule.Visibility);
        var now = clock.UtcNow;
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        var objectKey = MediaAsset.BuildObjectKey(purpose, currentUser.UserId, contentType, now);

        var asset = new MediaAsset
        {
            Bucket = bucket,
            ObjectKey = objectKey,
            ContentType = contentType,
            SizeBytes = request.SizeBytes,
            Purpose = purpose,
            UploadedBy = currentUser.UserId,
            CreatedAt = now,
        };

        // The row is written before the URL is handed out, so an object can never exist
        // in a bucket without a record of who put it there and why.
        var assetId = await mediaAssets.InsertAsync(asset, cancellationToken);

        var uploadUrl = storage.CreateUploadUrl(bucket, objectKey, contentType, UploadUrlLifetime);

        logger.LogInformation(
            "Presigned {Purpose} upload {AssetId} for user {UserId} into {Bucket}",
            purpose, assetId, currentUser.UserId, bucket);

        return new PresignUploadResponse(assetId, uploadUrl, objectKey, now.Add(UploadUrlLifetime));
    }

    public async Task<CommitUploadResponse> CommitAsync(
        long assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await mediaAssets.GetByIdAsync(assetId, cancellationToken)
            ?? throw new NotFoundException($"Upload {assetId} was not found.");

        if (asset.UploadedBy != currentUser.UserId)
        {
            // Not a 404: the caller supplied an id that exists, and pretending otherwise
            // would make a genuine client bug much harder to diagnose. The id is opaque
            // and unguessable in practice, so confirming existence leaks nothing useful.
            throw new ForbiddenException("This upload belongs to another user.");
        }

        var rule = MediaPurposePolicy.For(asset.Purpose);

        if (asset.State == MediaAssetState.Committed)
        {
            // Idempotent. Mobile clients retry commits on flaky connections and that is
            // normal traffic, not an error.
            return BuildResponse(asset, rule.Visibility);
        }

        var exists = await storage.ExistsAsync(asset.Bucket, asset.ObjectKey, cancellationToken);
        if (!exists)
        {
            throw new ConflictException(
                "The file has not finished uploading. Complete the upload before committing.");
        }

        var updated = await mediaAssets.MarkCommittedAsync(assetId, clock.UtcNow, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException($"Upload {assetId} was not found.");
        }

        logger.LogInformation("Committed {Purpose} upload {AssetId}", asset.Purpose, assetId);

        return BuildResponse(asset, rule.Visibility);
    }

    private CommitUploadResponse BuildResponse(MediaAsset asset, MediaVisibility visibility) =>
        new(asset.Id,
            visibility == MediaVisibility.Public
                ? storage.GetPublicUrl(asset.Bucket, asset.ObjectKey)
                : null);

    private static MediaPurpose ParsePurpose(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("purpose", "A purpose is required.");
        }

        // Accepts the snake_case wire form ("kyc_document"). Enum.TryParse would also
        // accept "KycDocument", which we do not want to become an accidental contract.
        foreach (var candidate in MediaPurposePolicy.AllPurposes)
        {
            if (string.Equals(Naming.ToSnakeCase(candidate.ToString()), value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        var known = string.Join(", ", MediaPurposePolicy.AllPurposes.Select(p => Naming.ToSnakeCase(p.ToString())));
        throw new ValidationException("purpose", $"Unknown purpose '{value}'. Expected one of: {known}.");
    }
}
