using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Reads and writes <see cref="User"/> rows.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new mobile user (phone-identified, no password) and returns its id.</summary>
    Task<long> InsertPhoneUserAsync(string phone, UserRole role, CancellationToken cancellationToken = default);
}
