using Microsoft.Extensions.Logging;
using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure.Auth;

/// <summary>
/// Stand-in <see cref="ISmsSender"/> that logs the OTP instead of sending a real SMS.
/// </summary>
/// <remarks>
/// <para>
/// <b>No SMS provider is wired up yet.</b> This is the only registered implementation of
/// <see cref="ISmsSender"/> today, in every environment including whatever runs in CI —
/// there is no environment gate here because there is, as yet, nothing to gate: swap the
/// DI registration in <c>DependencyInjection.AddInfrastructure</c> for a real Semaphore or
/// Movider adapter when one exists, and this class can be deleted. Nothing above
/// <see cref="ISmsSender"/> needs to change when that happens.
/// </para>
/// <para>
/// Logged at <see cref="LogLevel.Information"/> — deliberately visible by default, not
/// buried at Debug — since reading the code off the console is the only way to actually
/// complete a login while this stub is in place.
/// </para>
/// </remarks>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendOtpAsync(string phoneE164, string code, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[DEV SMS STUB — no real SMS provider wired up] OTP for {Phone}: {Code}",
            phoneE164, code);

        return Task.CompletedTask;
    }
}
