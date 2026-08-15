using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Auth;

/// <summary>Admin read/write access to the OTP verification bypass switch.</summary>
public sealed class OtpSettingsService(IOtpVerificationSettingsRepository settings, IClock clock)
{
    public async Task<OtpBypassSettingsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var current = await settings.GetAsync(cancellationToken);
        return new OtpBypassSettingsResponse(current?.BypassEnabled ?? false, current?.UpdatedAtUtc);
    }

    public async Task<OtpBypassSettingsResponse> SetBypassEnabledAsync(
        bool bypassEnabled,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        await settings.SetAsync(
            new OtpVerificationSettings { BypassEnabled = bypassEnabled, UpdatedAtUtc = now },
            cancellationToken);

        return new OtpBypassSettingsResponse(bypassEnabled, now);
    }
}
