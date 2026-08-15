using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OnlinePalengke.Api.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Api.Authentication;

/// <summary>
/// Development-only authentication that trusts two request headers.
/// </summary>
/// <remarks>
/// <para>
/// <b>This scheme grants any caller any identity and role they ask for.</b> It exists so
/// the API is exercisable before real phone-OTP authentication lands, and so integration
/// tests can act as an arbitrary role without minting tokens.
/// </para>
/// <para>
/// It is registered only when the environment is Development <i>and</i>
/// <c>Auth:EnableDevHeaderScheme</c> is explicitly true — two independent conditions, so
/// that neither an environment variable slip nor a stray config value is enough on its
/// own. Startup logs a warning whenever it is active. It must be deleted, not merely
/// disabled, once JWT authentication is in place.
/// </para>
/// <para>
/// Usage: <c>X-Dev-User-Id: 1</c> and <c>X-Dev-Role: partner</c>.
/// </para>
/// </remarks>
public sealed class DevHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "DevHeader";

    private const string UserIdHeader = "X-Dev-User-Id";
    private const string RoleHeader = "X-Dev-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var rawUserId)
            || !Request.Headers.TryGetValue(RoleHeader, out var rawRole))
        {
            // No headers means an anonymous request, not a failed one — endpoints that
            // allow anonymous access must still work.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!long.TryParse(rawUserId.ToString(), out var userId) || userId <= 0)
        {
            return Task.FromResult(AuthenticateResult.Fail($"{UserIdHeader} must be a positive integer."));
        }

        UserRole role;
        try
        {
            role = Naming.FromDbValue<UserRole>(rawRole.ToString());
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(AuthenticateResult.Fail(ex.Message));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(HttpCurrentUser.UserIdClaim, userId.ToString()),
                new Claim(HttpCurrentUser.RoleClaim, Naming.ToDbValue(role)),
                new Claim(ClaimTypes.Role, Naming.ToDbValue(role)),
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
