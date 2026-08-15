using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Infrastructure.Auth;

/// <summary>HMAC-SHA256-signed JWT access tokens.</summary>
/// <remarks>
/// Claim shape is deliberately identical to what <c>DevHeaderAuthenticationHandler</c>
/// produces — both a short <c>"role"</c> claim (what <c>HttpCurrentUser</c> and the
/// per-role authorization policies read) and the long-form <see cref="ClaimTypes.Role"/>
/// (what ASP.NET Core's own <c>[Authorize(Roles = ...)]</c> and <c>User.IsInRole()</c>
/// expect) — so every policy and every piece of code that reads the current user works
/// unchanged no matter which of the two schemes actually authenticated the request.
/// </remarks>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", Naming.ToDbValue(user.Role)),
            new(ClaimTypes.Role, Naming.ToDbValue(user.Role)),
        };

        if (!string.IsNullOrEmpty(user.Phone))
        {
            claims.Add(new Claim("phone", user.Phone));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(encoded, expiresAt);
    }
}
