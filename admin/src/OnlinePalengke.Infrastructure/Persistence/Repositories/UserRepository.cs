using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Infrastructure.Persistence;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="User"/>.</summary>
public sealed class UserRepository(DbSession session) : IUserRepository
{
    private const string SelectColumns =
        "id, phone, email, password_hash, role, status, created_at, updated_at";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM users WHERE id = @Id;";

    private const string SelectByPhoneSql =
        $"SELECT {SelectColumns} FROM users WHERE phone = @Phone;";

    private const string SelectByEmailSql =
        $"SELECT {SelectColumns} FROM users WHERE email = @Email;";

    private const string InsertPhoneUserSql = """
        INSERT INTO users (phone, role, status, created_at, updated_at)
        VALUES (@Phone, @Role, @Status, @Now, @Now);
        SELECT LAST_INSERT_ID();
        """;

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            SelectByPhoneSql,
            new { Phone = phone },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            SelectByEmailSql,
            new { Email = email },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertPhoneUserAsync(
        string phone,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertPhoneUserSql,
            new
            {
                Phone = phone,
                Role = Naming.ToDbValue(role),
                Status = Naming.ToDbValue(UserStatus.Active),
                Now = DateTime.UtcNow,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class UserRow
    {
        public long Id { get; init; }

        public string? Phone { get; init; }

        public string? Email { get; init; }

        public string? PasswordHash { get; init; }

        public string Role { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public User ToDomain() => new()
        {
            Id = Id,
            Phone = Phone,
            Email = Email,
            PasswordHash = PasswordHash,
            Role = Naming.FromDbValue<UserRole>(Role),
            Status = Naming.FromDbValue<UserStatus>(Status),
            CreatedAt = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
