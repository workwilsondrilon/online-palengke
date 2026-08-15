using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure.Auth;

/// <summary>bcrypt-backed <see cref="IPasswordHasher"/>.</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // 11 rounds: comfortably above bcrypt's usual 10-round baseline without making a
    // login request noticeably slow. Revisit upward as hardware gets faster.
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed stored hash should fail verification, not crash the request.
            return false;
        }
    }
}
