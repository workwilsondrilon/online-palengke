namespace OnlinePalengke.Application.Abstractions;

/// <summary>
/// Hashes and verifies short secrets a user supplies to authenticate — admin
/// passwords and OTP codes alike. Deliberately a slow, salted algorithm
/// (bcrypt) even for a six-digit OTP code: a fast hash gives essentially no
/// resistance to offline brute force against only a million possibilities.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
