namespace OnlinePalengke.Application.Abstractions;

/// <summary>Delivers a one-time password to a phone number.</summary>
/// <remarks>
/// The only implementation registered today (<c>LoggingSmsSender</c> in
/// Infrastructure) does not send a real SMS — it logs the code instead. That
/// is deliberate for this stage of the project, not an oversight: swap the
/// DI registration for a real provider (Semaphore or Movider) when one is
/// wired up, without anything above this interface needing to change.
/// </remarks>
public interface ISmsSender
{
    Task SendOtpAsync(string phoneE164, string code, CancellationToken cancellationToken = default);
}
