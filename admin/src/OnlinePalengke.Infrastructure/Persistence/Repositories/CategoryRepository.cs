using Dapper;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Infrastructure.Persistence.Repositories;

/// <summary>Dapper repository for <see cref="Category"/>.</summary>
public sealed class CategoryRepository(DbSession session) : ICategoryRepository
{
    private const string SelectColumns = "id, name, slug, sort_order, is_active, created_at, updated_at";

    private const string SelectAllSql =
        $"SELECT {SelectColumns} FROM categories ORDER BY sort_order, name;";

    private const string SelectByIdSql =
        $"SELECT {SelectColumns} FROM categories WHERE id = @Id;";

    private const string SelectBySlugSql =
        $"SELECT {SelectColumns} FROM categories WHERE slug = @Slug;";

    private const string InsertSql = """
        INSERT INTO categories (name, slug, sort_order, is_active, created_at, updated_at)
        VALUES (@Name, @Slug, @SortOrder, @IsActive, @CreatedAtUtc, @UpdatedAtUtc);
        SELECT LAST_INSERT_ID();
        """;

    private const string UpdateSql = """
        UPDATE categories
        SET name = @Name, slug = @Slug, sort_order = @SortOrder, is_active = @IsActive, updated_at = @UpdatedAtUtc
        WHERE id = @Id;
        """;

    private const string DeleteSql = "DELETE FROM categories WHERE id = @Id;";

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CategoryRow>(new CommandDefinition(
            SelectAllSql,
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Category?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(new CommandDefinition(
            SelectByIdSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(new CommandDefinition(
            SelectBySlugSql,
            new { Slug = slug },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<long> InsertAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        var connection = await session.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSql,
            new
            {
                category.Name,
                category.Slug,
                category.SortOrder,
                category.IsActive,
                category.CreatedAtUtc,
                category.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                category.Id,
                category.Name,
                category.Slug,
                category.SortOrder,
                category.IsActive,
                category.UpdatedAtUtc,
            },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var connection = await session.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            DeleteSql,
            new { Id = id },
            transaction: session.Transaction,
            commandTimeout: session.CommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    private sealed class CategoryRow
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Slug { get; init; } = string.Empty;

        public int SortOrder { get; init; }

        public bool IsActive { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }

        public Category ToDomain() => new()
        {
            Id = Id,
            Name = Name,
            Slug = Slug,
            SortOrder = SortOrder,
            IsActive = IsActive,
            // MySQL DATETIME carries no offset, so the value comes back Unspecified.
            // Everything in this schema is UTC by contract.
            CreatedAtUtc = DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc),
        };
    }
}
