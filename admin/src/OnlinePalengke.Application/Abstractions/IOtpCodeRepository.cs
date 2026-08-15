using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="OtpCode"/> rows.</summary>
public interface IOtpCodeRepository
{
    Task<long> InsertAsync(OtpCode code, CancellationToken cancellationToken = default);

    /// <summary>The most recently issued code for a phone, or null if none exists.</summary>
    Task<OtpCode?> GetLatestForPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>How many codes have been issued for this phone since <paramref name="sinceUtc"/> — the rate-limit signal.</summary>
    Task<int> CountIssuedSinceAsync(string phone, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Persists a failed-attempt increment.</summary>
    Task RecordFailedAttemptAsync(long id, int newAttemptCount, CancellationToken cancellationToken = default);

    /// <summary>Persists the code being marked consumed.</summary>
    Task MarkConsumedAsync(long id, DateTime consumedAtUtc, CancellationToken cancellationToken = default);
}
