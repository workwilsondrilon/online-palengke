using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes the single <see cref="OtpVerificationSettings"/> row.</summary>
public interface IOtpVerificationSettingsRepository
{
    /// <summary>Null when no admin has ever changed the setting — treat that as bypass off.</summary>
    Task<OtpVerificationSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts the single row: no prior row is required to exist.</summary>
    Task SetAsync(OtpVerificationSettings settings, CancellationToken cancellationToken = default);
}
