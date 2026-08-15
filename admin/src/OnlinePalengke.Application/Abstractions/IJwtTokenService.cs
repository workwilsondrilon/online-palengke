using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Issues and validates the API's own JWT access tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates a signed access token carrying the user's id and role.</summary>
    AccessToken CreateAccessToken(User user);
}

/// <param name="Value">The encoded JWT.</param>
/// <param name="ExpiresAtUtc">When it stops validating. Also the value returned to the client.</param>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
