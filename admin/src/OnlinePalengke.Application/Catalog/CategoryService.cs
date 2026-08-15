using System.Text.RegularExpressions;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Catalog;

/// <summary>Admin CRUD for catalog categories.</summary>
public sealed partial class CategoryService(ICategoryRepository categories, IClock clock)
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await categories.ListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.");

        return ToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireName(request.Name);
        var slug = RequireSlug(request.Slug);

        if (await categories.GetBySlugAsync(slug, cancellationToken) is not null)
        {
            throw new ConflictException($"A category with slug '{slug}' already exists.");
        }

        var now = clock.UtcNow;
        var category = new Category
        {
            Name = name,
            Slug = slug,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await categories.InsertAsync(category, cancellationToken);
        return new CategoryResponse(id, category.Name, category.Slug, category.SortOrder, category.IsActive);
    }

    public async Task<CategoryResponse> UpdateAsync(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.");

        var name = RequireName(request.Name);
        var slug = RequireSlug(request.Slug);

        var slugOwner = await categories.GetBySlugAsync(slug, cancellationToken);
        if (slugOwner is not null && slugOwner.Id != id)
        {
            throw new ConflictException($"A category with slug '{slug}' already exists.");
        }

        var updated = new Category
        {
            Id = id,
            Name = name,
            Slug = slug,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await categories.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Category {id} was not found.");
        }

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.");

        try
        {
            if (!await categories.DeleteAsync(id, cancellationToken))
            {
                throw new NotFoundException($"Category {id} was not found.");
            }
        }
        catch (Exception ex) when (IsForeignKeyViolation(ex))
        {
            throw new ConflictException(
                "This category still has items assigned to it. Move or remove them first.");
        }
    }

    /// <summary>
    /// True when the underlying provider exception is a foreign-key constraint violation.
    /// The Application layer has no MySqlConnector reference, so this matches on the
    /// standard ADO.NET shape (an <see cref="System.Data.Common.DbException"/> whose
    /// vendor-specific error number is MySQL's 1451, "Cannot delete or update a parent
    /// row") rather than a provider-specific exception type.
    /// </summary>
    internal static bool IsForeignKeyViolation(Exception ex) =>
        ex is System.Data.Common.DbException dbEx && dbEx.SqlState == "23000";

    private static CategoryResponse ToResponse(Category category) =>
        new(category.Id, category.Name, category.Slug, category.SortOrder, category.IsActive);

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A category name is required.");
        }

        return name.Trim();
    }

    private static string RequireSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || !SlugPattern().IsMatch(slug))
        {
            throw new ValidationException(nameof(slug), "Slug must be lowercase letters, digits and hyphens, e.g. 'fish-seafood'.");
        }

        return slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
