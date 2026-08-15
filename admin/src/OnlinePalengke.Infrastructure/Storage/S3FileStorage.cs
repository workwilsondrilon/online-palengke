using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Infrastructure.Storage;

/// <summary>AWS S3 implementation of <see cref="IFileStorage"/>.</summary>
public sealed class S3FileStorage(IAmazonS3 client, IOptions<S3Options> options) : IFileStorage
{
    private readonly S3Options _options = options.Value;

    public string ResolveBucket(MediaVisibility visibility) => visibility switch
    {
        MediaVisibility.Public => _options.PublicBucket,
        MediaVisibility.Private => _options.PrivateBucket,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown visibility."),
    };

    public string CreateUploadUrl(string bucket, string objectKey, string contentType, TimeSpan expiresIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        // ContentType is part of the signature, so the client must send exactly this
        // header on the PUT. That is what stops a caller presigning for image/jpeg and
        // then uploading an executable.
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        return client.GetPreSignedURL(request);
    }

    public string CreateReadUrl(string bucket, string objectKey, TimeSpan expiresIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        return client.GetPreSignedURL(request);
    }

    public string GetPublicUrl(string bucket, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        if (!string.Equals(bucket, _options.PublicBucket, StringComparison.Ordinal))
        {
            // A private object's plain URL would return 403, and handing one to a client
            // as though it were readable is a bug worth failing loudly on.
            throw new InvalidOperationException(
                $"'{bucket}' is not the public bucket. Private objects must be read through CreateReadUrl.");
        }

        var encodedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));

        return string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"https://{bucket}.s3.{_options.Region}.amazonaws.com/{encodedKey}"
            : $"{_options.PublicBaseUrl.TrimEnd('/')}/{encodedKey}";
    }

    public async Task<bool> ExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetObjectMetadataAsync(bucket, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default) =>
        client.DeleteObjectAsync(bucket, objectKey, cancellationToken);
}
