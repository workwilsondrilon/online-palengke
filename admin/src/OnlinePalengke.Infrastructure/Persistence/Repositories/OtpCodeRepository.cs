using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="OtpCode"/>.</summary>
public sealed class OtpCodeRepository(DbSession session) : IOtpCodeRepository
{
    private const string InsertSql = """
        INSERT INTO otp_codes (phone, code_hash, expires_at, attempt_count, created_at)
        VALUES (@Phone, @CodeHash, @ExpiresAtUtc, 0, @CreatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string SelectLatestForPhoneSql = """
        SELECT id, phone, code_hash, expires_at, consumed_at, attempt_count, created_at
        FROM otp_codes
        WHERE phone = @Phone
        ORDER BY created_at DESC, id DESC
        LIMIT 1;
        """;

    private const string CountIssuedSinceSql = """
        SELECT COUNT(*) FROM otp_codes WHERE phone = @Phone AND created_at >= @SinceUtc;
        """;

    private const string UpdateAttemptCountSql =
        "UPDATE otp_codes SET attempt_count = @AttemptCount WHERE id = @Id;";

    private const string MarkConsumedSql =
        "UPDATE otp_codes SET consumed_at = @ConsumedAtUtc WHERE id = @Id;";

    public async Task<long> InsertAsync(OtpCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new { code.Phone, code.CodeHash, code.ExpiresAtUtc, code.CreatedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<OtpCode?> GetLatestForPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<OtpCodeRow>(new CommandDefinition(
            SelectLatestForPhoneSql,
            new { Phone = phone },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<int> CountIssuedSinceAsync(
        string phone,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            CountIssuedSinceSql,
            new { Phone = phone, SinceUtc = sinceUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task RecordFailedAttemptAsync(
        long id,
        int newAttemptCount,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpdateAttemptCountSql,
            new { Id = id, AttemptCount = newAttemptCount },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task MarkConsumedAsync(long id, DateTime consumedAtUtc, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            MarkConsumedSql,
            new { Id = id, ConsumedAtUtc = consumedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class OtpCodeRow
    {
        public long Id { get; init; }

        public string Phone { get; init; } = string.Empty;

        public string CodeHash { get; init; } = string.Empty;

        public DateTime ExpiresAt { get; init; }

        public DateTime? ConsumedAt { get; init; }

        public int AttemptCount { get; init; }

        public DateTime CreatedAt { get; init; }

        public OtpCode ToDomain()
        {
            var otp = new OtpCode
            {
                Id = Id,
                Phone = Phone,
                CodeHash = CodeHash,
                ExpiresAtUtc = DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Utc),
                CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            };

            // Hydrated through the domain's own mutators, same convention as
            // MediaAssetRepository - no raw setter exists for either field, so a
            // freshly-read row reaches the exact state the domain already understands.
            for (var i = 0; i < AttemptCount; i++)
            {
                otp.RecordFailedAttempt();
            }

            if (ConsumedAt is { } consumedAt)
            {
                otp.MarkConsumed(DateTime.SpecifyKind(consumedAt, DateTimeKind.Utc));
            }

            return otp;
        }
    }
}
