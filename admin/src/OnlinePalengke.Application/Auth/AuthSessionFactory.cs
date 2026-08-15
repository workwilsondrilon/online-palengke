using System.Security.Cryptography;
using System.Text;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Auth;

/// <summary>
/// Issues an access + refresh token pair for an authenticated user.
/// </summary>
/// <remarks>
/// The one piece of logic <see cref="OtpAuthService"/> and <see cref="AdminAuthService"/>
/// would otherwise duplicate. Refresh tokens are opaque, high-entropy random
/// strings — not JWTs — hashed with a fast hash (SHA-256) before storage,
/// which is the correct trade-off here precisely because they are already
/// 256 bits of randomness: unlike a six-digit OTP code, there is no
/// meaningfully small space for an offline attacker to brute-force.
/// </remarks>
public sealed class AuthSessionFactory(
    IJwtTokenService jwtTokenService,
    IRefreshTokenRepository refreshTokens,
    IClock clock)
{
    public async Task<(AccessToken Access, string RefreshTokenPlain)> IssueAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var access = jwtTokenService.CreateAccessToken(user);
        var refreshPlain = GenerateOpaqueToken();
        var now = clock.UtcNow;

        await refreshTokens.InsertAsync(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(refreshPlain),
                ExpiresAtUtc = now.Add(RefreshToken.Lifetime),
                CreatedAtUtc = now,
            },
            cancellationToken);

        return (access, refreshPlain);
    }

    public static string GenerateOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string HashToken(string tokenPlain) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenPlain)));
}
