using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="RefreshToken"/>.</summary>
public sealed class RefreshTokenRepository(DbSession session) : IRefreshTokenRepository
{
    private const string InsertSql = """
        INSERT INTO refresh_tokens (user_id, token_hash, expires_at, created_at)
        VALUES (@UserId, @TokenHash, @ExpiresAtUtc, @CreatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string SelectByTokenHashSql = """
        SELECT id, user_id, token_hash, expires_at, revoked_at, created_at
        FROM refresh_tokens
        WHERE token_hash = @TokenHash;
        """;

    private const string RevokeSql =
        "UPDATE refresh_tokens SET revoked_at = @RevokedAtUtc WHERE id = @Id AND revoked_at IS NULL;";

    public async Task<long> InsertAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new { token.UserId, token.TokenHash, token.ExpiresAtUtc, token.CreatedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(new CommandDefinition(
            SelectByTokenHashSql,
            new { TokenHash = tokenHash },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task RevokeAsync(long id, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            RevokeSql,
            new { Id = id, RevokedAtUtc = revokedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class RefreshTokenRow
    {
        public long Id { get; init; }

        public long UserId { get; init; }

        public string TokenHash { get; init; } = string.Empty;

        public DateTime ExpiresAt { get; init; }

        public DateTime? RevokedAt { get; init; }

        public DateTime CreatedAt { get; init; }

        public RefreshToken ToDomain()
        {
            var token = new RefreshToken
            {
                Id = Id,
                UserId = UserId,
                TokenHash = TokenHash,
                ExpiresAtUtc = DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Utc),
                CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            };

            if (RevokedAt is { } revokedAt)
            {
                token.Revoke(DateTime.SpecifyKind(revokedAt, DateTimeKind.Utc));
            }

            return token;
        }
    }
}
