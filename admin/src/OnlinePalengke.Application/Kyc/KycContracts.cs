namespace OnlinePalengke.Application.Kyc;

public sealed record CreateDocumentTypeRequest(
    string Code,
    string Name,
    string AppliesToRole,
    long? AppliesToCategoryId,
    bool IsRequired,
    bool RequiresExpiry);

/// <summary>AppliesToRole is deliberately absent — stable once a type exists, same reasoning as <c>Unit.Code</c>.</summary>
public sealed record UpdateDocumentTypeRequest(
    string Code,
    string Name,
    long? AppliesToCategoryId,
    bool IsRequired,
    bool RequiresExpiry);

public sealed record DocumentTypeResponse(
    long Id,
    string Code,
    string Name,
    string AppliesToRole,
    long? AppliesToCategoryId,
    bool IsRequired,
    bool RequiresExpiry,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SubmitKycDocumentRequest(long DocumentTypeId, long MediaAssetId);

public sealed record ApproveKycDocumentRequest(DateTime? ExpiresAtUtc);

public sealed record RejectKycDocumentRequest(string Reason);

public sealed record KycDocumentResponse(
    long Id,
    string OwnerRole,
    long OwnerId,
    long DocumentTypeId,
    string DocumentTypeName,
    long MediaAssetId,
    string Status,
    DateTime SubmittedAtUtc,
    long? ReviewedBy,
    DateTime? ReviewedAtUtc,
    string? RejectionReason,
    DateTime? ExpiresAtUtc);

/// <summary>A short-lived presigned URL for viewing a private KYC document — see <see cref="OnlinePalengke.Application.Abstractions.IFileStorage.CreateReadUrl"/>.</summary>
public sealed record KycDocumentReadUrlResponse(string Url, DateTime ExpiresAtUtc);
