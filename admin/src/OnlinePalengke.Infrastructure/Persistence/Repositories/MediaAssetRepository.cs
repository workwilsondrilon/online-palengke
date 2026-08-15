using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper repository for <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// This is the reference implementation for every repository in the solution. The
/// conventions it demonstrates are not optional:
/// <list type="bullet">
/// <item>SQL lives in <c>private const string</c> fields at the top, never inline and
/// never concatenated. Every value is a parameter.</item>
/// <item>Every call passes <c>session.Transaction</c>, so the work joins the ambient
/// transaction. Omitting it is the classic silent bug — the write succeeds, then is not
/// rolled back with everything else.</item>
/// <item>Rows are read into a private row type and mapped explicitly to the domain
/// entity. Nothing weakly-typed crosses this boundary, and the entity keeps its
/// invariants instead of having its state poked in by the mapper.</item>
/// </list>
/// </remarks>
public sealed class MediaAssetRepository(DbSession session) : IMediaAssetRepository
{
    private const string InsertSql = """
        INSERT INTO media_assets
            (bucket, object_key, content_type, size_bytes, purpose, uploaded_by, state, created_at)
        VALUES
            (@Bucket, @ObjectKey, @ContentType, @SizeBytes, @Purpose, @UploadedBy, @State, @CreatedAt);
        SELECT LAST_INSERT_ID();
        """;

    private const string SelectByIdSql = """
        SELECT id, bucket, object_key, content_type, size_bytes, purpose,
               uploaded_by, state, created_at, committed_at
        FROM media_assets
        WHERE id = @Id;
        """;

    private const string MarkCommittedSql = """
        UPDATE media_assets
        SET state = 'committed', committed_at = @CommittedAt
        WHERE id = @Id AND state = 'pending';
        """;

    private const string SelectStalePendingSql = """
        SELECT id, bucket, object_key, content_type, size_bytes, purpose,
               uploaded_by, state, created_at, committed_at
        FROM media_assets
        WHERE state = 'pending' AND created_at < @OlderThan
        ORDER BY created_at
        LIMIT @Limit;
        """;

    private const string DeleteSql = "DELETE FROM media_assets WHERE id = @Id;";

    public async Task<long> InsertAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                asset.Bucket,
                asset.ObjectKey,
                asset.ContentType,
                asset.SizeBytes,
                Purpose = Naming.ToDbValue(asset.Purpose),
                asset.UploadedBy,
                State = Naming.ToDbValue(asset.State),
                asset.CreatedAt,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<MediaAsset?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MediaAssetRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<bool> MarkCommittedAsync(
        long id,
        DateTime committedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            MarkCommittedSql,
            new { Id = id, CommittedAt = committedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        // Zero rows means either the id is gone or it was already committed. The caller
        // has already handled the already-committed case, so treat this as "not found".
        return affected > 0;
    }

    public async Task<IReadOnlyList<MediaAsset>> GetStalePendingAsync(
        DateTime olderThanUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MediaAssetRow>(new CommandDefinition(
            SelectStalePendingSql,
            new { OlderThan = olderThanUtc, Limit = limit },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    /// <summary>
    /// Flat shape matching the SELECT list. Kept private so the weakly-invariant form
    /// never escapes the repository.
    /// </summary>
    private sealed class MediaAssetRow
    {
        public long Id { get; init; }

        public string Bucket { get; init; } = string.Empty;

        public string ObjectKey { get; init; } = string.Empty;

        public string ContentType { get; init; } = string.Empty;

        public long SizeBytes { get; init; }

        public string Purpose { get; init; } = string.Empty;

        public long UploadedBy { get; init; }

        public string State { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime? CommittedAt { get; init; }

        public MediaAsset ToDomain()
        {
            var asset = new MediaAsset
            {
                Id = Id,
                Bucket = Bucket,
                ObjectKey = ObjectKey,
                ContentType = ContentType,
                SizeBytes = SizeBytes,
                Purpose = Naming.FromDbValue<MediaPurpose>(Purpose),
                UploadedBy = UploadedBy,
                CreatedAt = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            };

            // MySQL DATETIME carries no offset, so the value comes back Unspecified.
            // Everything in this schema is stored UTC by contract; stamping the kind here
            // stops it being treated as local time the moment it is formatted or compared.
            if (Naming.FromDbValue<MediaAssetState>(State) == MediaAssetState.Committed)
            {
                asset.Commit(DateTime.SpecifyKind(
                    CommittedAt ?? CreatedAt, DateTimeKind.Utc));
            }

            return asset;
        }
    }
}
