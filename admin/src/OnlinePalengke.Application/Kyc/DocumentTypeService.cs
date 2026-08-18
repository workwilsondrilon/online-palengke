using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Catalog;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Kyc;

namespace OnlinePalengke.Application.Kyc;

/// <summary>Admin CRUD for the document types a partner or rider must submit to become verified.</summary>
public sealed class DocumentTypeService(
    IDocumentTypeRepository documentTypes, ICategoryRepository categories, IClock clock)
{
    public async Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await documentTypes.ListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<DocumentTypeResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var documentType = await documentTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document type {id} was not found.");

        return ToResponse(documentType);
    }

    public async Task<DocumentTypeResponse> CreateAsync(
        CreateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var code = RequireCode(request.Code);
        var name = RequireName(request.Name);
        var role = ParseApplicableRole(request.AppliesToRole);

        if (await documentTypes.GetByCodeAsync(code, cancellationToken) is not null)
        {
            throw new ConflictException($"A document type with code '{code}' already exists.");
        }

        var categoryId = await ResolveCategoryIdAsync(role, request.AppliesToCategoryId, cancellationToken);

        var now = clock.UtcNow;
        var documentType = new DocumentType
        {
            Code = code,
            Name = name,
            AppliesToRole = role,
            AppliesToCategoryId = categoryId,
            IsRequired = request.IsRequired,
            RequiresExpiry = request.RequiresExpiry,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var id = await documentTypes.InsertAsync(documentType, cancellationToken);
        return new DocumentTypeResponse(
            id, documentType.Code, documentType.Name, Naming.ToDbValue(documentType.AppliesToRole),
            documentType.AppliesToCategoryId, documentType.IsRequired, documentType.RequiresExpiry,
            documentType.CreatedAtUtc, documentType.UpdatedAtUtc);
    }

    public async Task<DocumentTypeResponse> UpdateAsync(
        long id, UpdateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await documentTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document type {id} was not found.");

        var code = RequireCode(request.Code);
        var name = RequireName(request.Name);

        var codeOwner = await documentTypes.GetByCodeAsync(code, cancellationToken);
        if (codeOwner is not null && codeOwner.Id != id)
        {
            throw new ConflictException($"A document type with code '{code}' already exists.");
        }

        var categoryId = await ResolveCategoryIdAsync(existing.AppliesToRole, request.AppliesToCategoryId, cancellationToken);

        var updated = new DocumentType
        {
            Id = id,
            Code = code,
            Name = name,
            AppliesToRole = existing.AppliesToRole,
            AppliesToCategoryId = categoryId,
            IsRequired = request.IsRequired,
            RequiresExpiry = request.RequiresExpiry,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = clock.UtcNow,
        };

        if (!await documentTypes.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Document type {id} was not found.");
        }

        return ToResponse(updated);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _ = await documentTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document type {id} was not found.");

        try
        {
            if (!await documentTypes.DeleteAsync(id, cancellationToken))
            {
                throw new NotFoundException($"Document type {id} was not found.");
            }
        }
        catch (Exception ex) when (CategoryService.IsForeignKeyViolation(ex))
        {
            throw new ConflictException(
                "This document type has KYC submissions recorded against it and cannot be deleted.");
        }
    }

    private async Task<long?> ResolveCategoryIdAsync(
        UserRole role, long? requestedCategoryId, CancellationToken cancellationToken)
    {
        if (requestedCategoryId is null)
        {
            return null;
        }

        if (role != UserRole.Partner)
        {
            throw new ValidationException(
                nameof(CreateDocumentTypeRequest.AppliesToCategoryId),
                "Only a partner document type can be scoped to a category — riders have no category concept.");
        }

        if (await categories.GetByIdAsync(requestedCategoryId.Value, cancellationToken) is null)
        {
            throw new ValidationException(
                nameof(CreateDocumentTypeRequest.AppliesToCategoryId), "That category does not exist.");
        }

        return requestedCategoryId;
    }

    private static UserRole ParseApplicableRole(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            foreach (var candidate in new[] { UserRole.Partner, UserRole.Rider })
            {
                if (string.Equals(Naming.ToDbValue(candidate), role.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        throw new ValidationException(
            nameof(CreateDocumentTypeRequest.AppliesToRole), "Must be 'partner' or 'rider'.");
    }

    private static DocumentTypeResponse ToResponse(DocumentType documentType) => new(
        documentType.Id,
        documentType.Code,
        documentType.Name,
        Naming.ToDbValue(documentType.AppliesToRole),
        documentType.AppliesToCategoryId,
        documentType.IsRequired,
        documentType.RequiresExpiry,
        documentType.CreatedAtUtc,
        documentType.UpdatedAtUtc);

    private static string RequireCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(nameof(code), "A code is required.");
        }

        return code.Trim();
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A name is required.");
        }

        return name.Trim();
    }
}
