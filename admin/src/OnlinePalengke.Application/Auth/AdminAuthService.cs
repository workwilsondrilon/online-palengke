using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Auth;

/// <summary>Email + password login for the admin web app.</summary>
/// <remarks>
/// Admin accounts are provisioned out of band (there is no self-registration
/// endpoint — creating one is a deliberate future decision, not an oversight)
/// so this service only ever authenticates, never creates a user.
/// </remarks>
public sealed class AdminAuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    AuthSessionFactory sessionFactory)
{
    public async Task<AdminLoginResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw InvalidCredentials();
        }

        var user = await users.GetByEmailAsync(email.Trim(), cancellationToken);

        // Same failure for "no such account" and "wrong password" - confirming an email
        // exists via a different error message is exactly the enumeration leak this guards
        // against. Hash verification still runs a real comparison either way (via a dummy
        // hash) so the two paths take comparable time.
        if (user is null || user.Role != UserRole.Admin || user.PasswordHash is null)
        {
            hasher.Verify(password, DummyHashForTimingParity);
            throw InvalidCredentials();
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            throw InvalidCredentials();
        }

        if (!user.CanAuthenticate)
        {
            throw new ForbiddenException("This account is suspended.");
        }

        var (access, refreshPlain) = await sessionFactory.IssueAsync(user, cancellationToken);

        return new AdminLoginResponse(
            access.Value,
            refreshPlain,
            access.ExpiresAtUtc,
            new AdminUserResponse(user.Id, user.Email ?? email));
    }

    // A precomputed bcrypt hash of an arbitrary string - never a real password - used only
    // to keep the "no such account" path's timing comparable to a real verification.
    private const string DummyHashForTimingParity =
        "$2a$11$C6UzMDM.H6dfI/f/IKcEeO7isLdZAWjuU3PmuGwR1uGf8Wq1i3n7C";

    private static ValidationException InvalidCredentials() =>
        new("Incorrect email or password.");
}
