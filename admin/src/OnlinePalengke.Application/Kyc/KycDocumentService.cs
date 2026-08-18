using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Application.Onboarding;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Kyc;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.Application.Kyc;

/// <summary>
/// The KYC submit/review workflow: a partner or rider submits a document, an admin
/// approves or rejects it, and approvals are re-checked against every required document
/// type to decide whether the owning partner/rider should become verified.
/// </summary>
/// <remarks>
/// A resubmission always inserts a new <see cref="KycDocument"/> row rather than mutating
/// a rejected one — see <see cref="IKycDocumentRepository"/>'s remarks — so "the current
/// state of a requirement" always means "the latest submitted row for that owner and
/// document type", computed fresh in <see cref="EvaluateVerificationAsync"/> rather than
/// tracked as running state anywhere.
/// </remarks>
public sealed class KycDocumentService(
    IKycDocumentRepository kycDocuments,
    IDocumentTypeRepository documentTypes,
    IPartnerRepository partners,
    IRiderRepository riders,
    IMediaAssetRepository mediaAssets,
    IFileStorage fileStorage,
    PartnerService partnerService,
    RiderService riderService,
    ICurrentUser currentUser,
    IClock clock)
{
    private static readonly TimeSpan ReadUrlLifetime = TimeSpan.FromMinutes(5);

    public async Task<KycDocumentResponse> SubmitAsync(
        SubmitKycDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var (ownerRole, ownerId) = await ResolveOwnerAsync(cancellationToken);

        var documentType = await documentTypes.GetByIdAsync(request.DocumentTypeId, cancellationToken)
            ?? throw new NotFoundException($"Document type {request.DocumentTypeId} was not found.");

        if (documentType.AppliesToRole != ownerRole)
        {
            throw new ValidationException(
                nameof(request.DocumentTypeId), "This document type does not apply to your role.");
        }

        var asset = await mediaAssets.GetByIdAsync(request.MediaAssetId, cancellationToken)
            ?? throw new NotFoundException($"Upload {request.MediaAssetId} was not found.");

        if (asset.UploadedBy != currentUser.UserId)
        {
            throw new ForbiddenException("That upload does not belong to you.");
        }

        if (asset.State != MediaAssetState.Committed)
        {
            throw new ValidationException(
                nameof(request.MediaAssetId), "Finish uploading the file before submitting it.");
        }

        if (asset.Purpose != MediaPurpose.KycDocument)
        {
            throw new ValidationException(
                nameof(request.MediaAssetId), "That upload was not made for a KYC document.");
        }

        var now = clock.UtcNow;
        var document = new KycDocument
        {
            OwnerRole = ownerRole,
            OwnerId = ownerId,
            DocumentTypeId = request.DocumentTypeId,
            MediaAssetId = request.MediaAssetId,
            Status = KycDocumentStatus.Submitted,
            SubmittedAtUtc = now,
        };

        var id = await kycDocuments.InsertAsync(document, cancellationToken);

        // Only promotes a first-time PendingKyc profile -- see the method's own remarks
        // for why a later resubmission must not disturb an already-verified owner.
        if (ownerRole == UserRole.Partner)
        {
            await partnerService.MarkUnderReviewIfPendingAsync(ownerId, cancellationToken);
        }
        else
        {
            await riderService.MarkUnderReviewIfPendingAsync(ownerId, cancellationToken);
        }

        return ToResponse(document, documentType, id);
    }

    public async Task<IReadOnlyList<KycDocumentResponse>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var (ownerRole, ownerId) = await ResolveOwnerAsync(cancellationToken);
        var documents = await kycDocuments.ListLatestForOwnerAsync(ownerRole, ownerId, cancellationToken);
        return await ToResponsesAsync(documents, cancellationToken);
    }

    public async Task<IReadOnlyList<KycDocumentResponse>> ListPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        var documents = await kycDocuments.ListPendingReviewAsync(cancellationToken);
        return await ToResponsesAsync(documents, cancellationToken);
    }

    public async Task<KycDocumentResponse> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var document = await kycDocuments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"KYC document {id} was not found.");

        var documentType = await documentTypes.GetByIdAsync(document.DocumentTypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Document type {document.DocumentTypeId} referenced by KYC document {id} is missing.");

        return ToResponse(document, documentType, document.Id);
    }

    /// <summary>Admin viewing: a short-lived presigned GET for the underlying private object.</summary>
    public async Task<KycDocumentReadUrlResponse> GetReadUrlAsync(long id, CancellationToken cancellationToken = default)
    {
        var document = await kycDocuments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"KYC document {id} was not found.");

        var asset = await mediaAssets.GetByIdAsync(document.MediaAssetId, cancellationToken)
            ?? throw new InvalidOperationException($"Media asset {document.MediaAssetId} referenced by KYC document {id} is missing.");

        var url = fileStorage.CreateReadUrl(asset.Bucket, asset.ObjectKey, ReadUrlLifetime);
        return new KycDocumentReadUrlResponse(url, clock.UtcNow.Add(ReadUrlLifetime));
    }

    public async Task<KycDocumentResponse> ApproveAsync(
        long id, ApproveKycDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await kycDocuments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"KYC document {id} was not found.");

        if (existing.Status != KycDocumentStatus.Submitted)
        {
            throw new ConflictException("Only a submitted document can be approved.");
        }

        var documentType = await documentTypes.GetByIdAsync(existing.DocumentTypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Document type {existing.DocumentTypeId} referenced by KYC document {id} is missing.");

        if (documentType.RequiresExpiry && request.ExpiresAtUtc is null)
        {
            throw new ValidationException(
                nameof(request.ExpiresAtUtc), "This document type requires an expiry date.");
        }

        var updated = new KycDocument
        {
            Id = existing.Id,
            OwnerRole = existing.OwnerRole,
            OwnerId = existing.OwnerId,
            DocumentTypeId = existing.DocumentTypeId,
            MediaAssetId = existing.MediaAssetId,
            Status = KycDocumentStatus.Approved,
            SubmittedAtUtc = existing.SubmittedAtUtc,
            ReviewedBy = currentUser.UserId,
            ReviewedAtUtc = clock.UtcNow,
            RejectionReason = null,
            ExpiresAtUtc = request.ExpiresAtUtc,
        };

        if (!await kycDocuments.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"KYC document {id} was not found.");
        }

        await EvaluateVerificationAsync(updated.OwnerRole, updated.OwnerId, cancellationToken);

        return ToResponse(updated, documentType, updated.Id);
    }

    public async Task<KycDocumentResponse> RejectAsync(
        long id, RejectKycDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await kycDocuments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"KYC document {id} was not found.");

        if (existing.Status != KycDocumentStatus.Submitted)
        {
            throw new ConflictException("Only a submitted document can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(nameof(request.Reason), "A rejection reason is required.");
        }

        var updated = new KycDocument
        {
            Id = existing.Id,
            OwnerRole = existing.OwnerRole,
            OwnerId = existing.OwnerId,
            DocumentTypeId = existing.DocumentTypeId,
            MediaAssetId = existing.MediaAssetId,
            Status = KycDocumentStatus.Rejected,
            SubmittedAtUtc = existing.SubmittedAtUtc,
            ReviewedBy = currentUser.UserId,
            ReviewedAtUtc = clock.UtcNow,
            RejectionReason = request.Reason.Trim(),
            ExpiresAtUtc = null,
        };

        if (!await kycDocuments.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"KYC document {id} was not found.");
        }

        var documentType = await documentTypes.GetByIdAsync(updated.DocumentTypeId, cancellationToken)
            ?? throw new InvalidOperationException($"Document type {updated.DocumentTypeId} referenced by KYC document {id} is missing.");

        return ToResponse(updated, documentType, updated.Id);
    }

    /// <summary>
    /// Flips an approved-but-lapsed document to expired and suspends the owning
    /// partner/rider. Exists now so the daily expiry job (task #39) has a single, already-
    /// verified place to call rather than duplicating the state transition.
    /// </summary>
    public async Task ExpireAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await kycDocuments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"KYC document {id} was not found.");

        if (existing.Status != KycDocumentStatus.Approved)
        {
            throw new ConflictException("Only an approved document can expire.");
        }

        var updated = new KycDocument
        {
            Id = existing.Id,
            OwnerRole = existing.OwnerRole,
            OwnerId = existing.OwnerId,
            DocumentTypeId = existing.DocumentTypeId,
            MediaAssetId = existing.MediaAssetId,
            Status = KycDocumentStatus.Expired,
            SubmittedAtUtc = existing.SubmittedAtUtc,
            ReviewedBy = existing.ReviewedBy,
            ReviewedAtUtc = existing.ReviewedAtUtc,
            RejectionReason = existing.RejectionReason,
            ExpiresAtUtc = existing.ExpiresAtUtc,
        };

        if (!await kycDocuments.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"KYC document {id} was not found.");
        }

        if (existing.OwnerRole == UserRole.Partner)
        {
            await partnerService.MarkSuspendedAsync(existing.OwnerId, cancellationToken);
        }
        else
        {
            await riderService.MarkSuspendedAsync(existing.OwnerId, cancellationToken);
        }
    }

    /// <summary>
    /// Promotes the owning partner/rider to verified once every required document type for
    /// their role (and, for a partner, their category) has an approved, unexpired latest
    /// submission. Never demotes -- a lapse is handled separately by
    /// <see cref="ExpireAsync"/>, not by this check finding a gap.
    /// </summary>
    private async Task EvaluateVerificationAsync(UserRole ownerRole, long ownerId, CancellationToken cancellationToken)
    {
        long? categoryId = null;
        if (ownerRole == UserRole.Partner)
        {
            var partner = await partners.GetByIdAsync(ownerId, cancellationToken);
            categoryId = partner?.CategoryId;
        }

        var requiredTypes = (await documentTypes.ListForRoleAsync(ownerRole, categoryId, cancellationToken))
            .Where(dt => dt.IsRequired)
            .ToList();

        if (requiredTypes.Count == 0)
        {
            return;
        }

        var latestByType = (await kycDocuments.ListLatestForOwnerAsync(ownerRole, ownerId, cancellationToken))
            .ToDictionary(d => d.DocumentTypeId);

        var now = clock.UtcNow;
        var allSatisfied = requiredTypes.All(rt =>
            latestByType.TryGetValue(rt.Id, out var latest)
            && latest.Status == KycDocumentStatus.Approved
            && (latest.ExpiresAtUtc is null || latest.ExpiresAtUtc > now));

        if (!allSatisfied)
        {
            return;
        }

        if (ownerRole == UserRole.Partner)
        {
            await partnerService.MarkVerifiedAsync(ownerId, cancellationToken);
        }
        else
        {
            await riderService.MarkVerifiedAsync(ownerId, cancellationToken);
        }
    }

    /// <summary>Resolves the calling partner/rider's own profile id from their authenticated identity.</summary>
    private async Task<(UserRole Role, long OwnerId)> ResolveOwnerAsync(CancellationToken cancellationToken)
    {
        switch (currentUser.Role)
        {
            case UserRole.Partner:
                var partner = await partners.GetByUserIdAsync(currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException("Register as a partner before submitting KYC documents.");
                return (UserRole.Partner, partner.Id);

            case UserRole.Rider:
                var rider = await riders.GetByUserIdAsync(currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException("Register as a rider before submitting KYC documents.");
                return (UserRole.Rider, rider.Id);

            default:
                throw new ForbiddenException("Only partners and riders submit KYC documents.");
        }
    }

    private async Task<IReadOnlyList<KycDocumentResponse>> ToResponsesAsync(
        IReadOnlyList<KycDocument> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var types = await documentTypes.ListAsync(cancellationToken);
        var typesById = types.ToDictionary(t => t.Id);

        return documents
            .Select(d => ToResponse(d, typesById[d.DocumentTypeId], d.Id))
            .ToList();
    }

    private static KycDocumentResponse ToResponse(KycDocument document, DocumentType documentType, long id) => new(
        id,
        Naming.ToDbValue(document.OwnerRole),
        document.OwnerId,
        document.DocumentTypeId,
        documentType.Name,
        document.MediaAssetId,
        Naming.ToDbValue(document.Status),
        document.SubmittedAtUtc,
        document.ReviewedBy,
        document.ReviewedAtUtc,
        document.RejectionReason,
        document.ExpiresAtUtc);
}
