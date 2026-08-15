using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>
/// Object storage. Backed by AWS S3 in every environment, including local development.
/// </summary>
/// <remarks>
/// Bytes never pass through the API. Clients upload straight to S3 with a presigned PUT
/// and read private objects with a short-TTL presigned GET, which keeps large files off
/// the application's request pipeline entirely.
/// </remarks>
public interface IFileStorage
{
    /// <summary>Maps a purpose's visibility to the concrete bucket name for this environment.</summary>
    string ResolveBucket(MediaVisibility visibility);

    /// <summary>
    /// Issues a presigned PUT URL. The signature binds the content type and the key, so a
    /// client cannot upload something other than what it declared, nor write to another key.
    /// </summary>
    string CreateUploadUrl(string bucket, string objectKey, string contentType, TimeSpan expiresIn);

    /// <summary>
    /// Issues a short-TTL presigned GET URL for a private object. This is the only way
    /// KYC documents and delivery proof photos are ever read.
    /// </summary>
    string CreateReadUrl(string bucket, string objectKey, TimeSpan expiresIn);

    /// <summary>
    /// The plain, unsigned URL for an object in the public-read bucket. Never call this
    /// for a private object — it would produce a URL that returns 403.
    /// </summary>
    string GetPublicUrl(string bucket, string objectKey);

    /// <summary>
    /// Whether the object actually landed. The commit endpoint uses this so a client
    /// cannot mark an asset committed without having uploaded anything.
    /// </summary>
    Task<bool> ExistsAsync(string bucket, string objectKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default);
}
