namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// Platform-wide, admin-toggled switch for whether phone-OTP verification is bypassed.
/// </summary>
/// <remarks>
/// Exists because no real SMS provider is wired up yet — <c>LoggingSmsSender</c> only
/// logs the code, so completing login otherwise means reading it out of server logs.
/// With bypass on, <c>OtpAuthService.VerifyOtpAsync</c> accepts any submitted code once a
/// code has genuinely been requested (the request/rate-limit/expiry machinery is
/// untouched — only the code-correctness check is skipped), unblocking QA and demos.
/// Delete this once m360 (the intended SMS provider) is integrated and bypass is no
/// longer needed.
/// </remarks>
public sealed class OtpVerificationSettings
{
    public required bool BypassEnabled { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
