using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OnlinePalengke.Api.Authentication;

/// <summary>
/// A scheme that never authenticates anyone.
/// </summary>
/// <remarks>
/// Registered when the development header scheme is switched off and real JWT
/// authentication does not exist yet. Its purpose is to make the default posture a
/// clean 401 on every protected endpoint. Registering no scheme at all would instead
/// throw an unhelpful "no authentication scheme was specified" exception at request
/// time, which reads like a bug rather than the intended refusal.
/// </remarks>
public sealed class DenyAllAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
