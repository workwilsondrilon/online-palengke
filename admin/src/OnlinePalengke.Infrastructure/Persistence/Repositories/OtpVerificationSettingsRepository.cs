using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for the single <see cref="OtpVerificationSettings"/> row.</summary>
public sealed class OtpVerificationSettingsRepository(DbSession session) : IOtpVerificationSettingsRepository
{
    private const string SelectSql =
        "SELECT bypass_enabled, updated_at FROM otp_verification_settings WHERE id = 1;";

    private const string UpsertSql = """
        INSERT INTO otp_verification_settings (id, bypass_enabled, updated_at)
        VALUES (1, @BypassEnabled, @UpdatedAtUtc)
        ON DUPLICATE KEY UPDATE bypass_enabled = @BypassEnabled, updated_at = @UpdatedAtUtc;
        """;

    public async Task<OtpVerificationSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(new CommandDefinition(
            SelectSql,
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task SetAsync(OtpVerificationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var connection = await session.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new { settings.BypassEnabled, settings.UpdatedAtUtc },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class SettingsRow
    {
        public bool BypassEnabled { get; init; }

        public DateTime UpdatedAt { get; init; }

        public OtpVerificationSettings ToDomain() => new()
        {
            BypassEnabled = BypassEnabled,
            // MySQL DATETIME carries no offset, so the value comes back Unspecified.
            // Everything in this schema is UTC by contract.
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
