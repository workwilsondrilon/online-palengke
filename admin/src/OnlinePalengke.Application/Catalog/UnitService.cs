using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Catalog;

namespace OnlinePalengke.Application.Catalog;

/// <summary>Admin CRUD for units of measure.</summary>
public sealed class UnitService(IUnitRepository units, IClock clock)
{
    public async Task<IReadOnlyList<UnitResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await units.ListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<UnitResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var unit = await units.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Unit {id} was not found.");

        return ToResponse(unit);
    }

    public async Task<UnitResponse> CreateAsync(CreateUnitRequest request, CancellationToken cancellationToken = default)
    {
        var code = RequireCode(request.Code);
        var name = RequireName(request.Name);

        if (await units.GetByCodeAsync(code, cancellationToken) is not null)
        {
            throw new ConflictException($"A unit with code '{code}' already exists.");
        }

        var now = clock.UtcNow;
        var unit = new Unit
        {
            Code = code,
            Name = name,
            AllowsFractionalQuantity = request.AllowsFractionalQuantity,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await units.InsertAsync(unit, cancellationToken);
        return new UnitResponse(id, unit.Code, unit.Name, unit.AllowsFractionalQuantity, unit.IsActive);
    }

    public async Task<UnitResponse> UpdateAsync(
        long id,
        UpdateUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await units.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Unit {id} was not found.");

        var code = RequireCode(request.Code);
        var name = RequireName(request.Name);

        var codeOwner = await units.GetByCodeAsync(code, cancellationToken);
        if (codeOwner is not null && codeOwner.Id != id)
        {
            throw new ConflictException($"A unit with code '{code}' already exists.");
        }

        var updated = new Unit
        {
            Id = id,
            Code = code,
            Name = name,
            AllowsFractionalQuantity = request.AllowsFractionalQuantity,
            IsActive = request.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await units.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Unit {id} was not found.");
        }

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await units.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Unit {id} was not found.");

        try
        {
            if (!await units.DeleteAsync(id, cancellationToken))
            {
                throw new NotFoundException($"Unit {id} was not found.");
            }
        }
        catch (Exception ex) when (CategoryService.IsForeignKeyViolation(ex))
        {
            throw new ConflictException("This unit is still assigned to one or more items. Remove it from those items first.");
        }
    }

    private static UnitResponse ToResponse(Unit unit) =>
        new(unit.Id, unit.Code, unit.Name, unit.AllowsFractionalQuantity, unit.IsActive);

    private static string RequireCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(nameof(code), "A unit code is required.");
        }

        return code.Trim().ToLowerInvariant();
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A unit name is required.");
        }

        return name.Trim();
    }
}
