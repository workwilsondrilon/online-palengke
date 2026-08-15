namespace OnlinePalengke.Application.Uploads;

/// <summary>
/// A client's request for permission to upload one file.
/// </summary>
/// <param name="Purpose">
/// snake_case purpose, e.g. <c>kyc_document</c>. Decides the bucket, the size ceiling
/// and the accepted content types — the client does not get to choose any of those.
/// </param>
/// <param name="ContentType">The exact MIME type the client will send. Signed into the URL.</param>
/// <param name="SizeBytes">
/// The declared size. Checked against the purpose's ceiling before a URL is issued, so
/// an oversized upload is refused before any bytes cross the network.
/// </param>
public sealed record PresignUploadRequest(string Purpose, string ContentType, long SizeBytes);

/// <summary>Permission to upload, plus the handle needed to confirm it afterwards.</summary>
/// <param name="AssetId">Pass this back to the commit endpoint, and store it on the owning row.</param>
/// <param name="UploadUrl">
/// Presigned PUT target. The client sends bytes here directly and must NOT attach its
/// Authorization header — an extra header breaks the signature and S3 rejects the request.
/// </param>
/// <param name="ObjectKey">Server-generated key. Returned for logging and diagnostics only.</param>
/// <param name="ExpiresAtUtc">After this the URL is dead and the client must ask again.</param>
public sealed record PresignUploadResponse(
    long AssetId,
    string UploadUrl,
    string ObjectKey,
    DateTime ExpiresAtUtc);

/// <summary>Confirmation that an upload landed and is now referenceable.</summary>
/// <param name="AssetId">The committed asset.</param>
/// <param name="PublicUrl">
/// The directly-readable URL for public assets. Null for private ones — KYC documents
/// and delivery proof are only ever read through a short-TTL presigned GET.
/// </param>
public sealed record CommitUploadResponse(long AssetId, string? PublicUrl);
