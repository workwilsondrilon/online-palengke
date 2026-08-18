using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Application.Common;
using OnlinePalengke.Application.Storefront;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Moderation;

namespace OnlinePalengke.Application.Moderation;

/// <summary>
/// Reactive content moderation: any user can report a live partner product, an admin
/// reviews the report feed, and can either dismiss it, resolve it as fine, or take the
/// content down — takedown unpublishes via the same path a partner uses to unpublish their
/// own product, not a separate deletion.
/// </summary>
public sealed class ContentModerationService(
    IContentReportRepository reports,
    PartnerProductService productService,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<ContentReportResponse> ReportAsync(
        ReportContentRequest request, CancellationToken cancellationToken = default)
    {
        var targetType = ParseTargetType(request.TargetType);
        var reason = RequireReason(request.Reason);

        if (targetType == ContentTargetType.PartnerProduct)
        {
            // Throws NotFoundException if the product doesn't exist -- a report against
            // nothing is not a meaningful thing to record.
            _ = await productService.GetAsync(request.TargetId, cancellationToken);
        }

        var report = new ContentReport
        {
            ReporterUserId = currentUser.UserId,
            TargetType = targetType,
            TargetId = request.TargetId,
            Reason = reason,
            Status = ContentReportStatus.Open,
            CreatedAtUtc = clock.UtcNow,
        };

        var id = await reports.InsertAsync(report, cancellationToken);
        return ToResponse(report, id);
    }

    public async Task<IReadOnlyList<ContentReportResponse>> ListAsync(
        ContentReportStatus? status = null, CancellationToken cancellationToken = default)
    {
        var rows = await reports.ListAsync(status, cancellationToken);
        return rows.Select(r => ToResponse(r, r.Id)).ToList();
    }

    /// <summary>The reported content was fine — closes the report with no action against it.</summary>
    public Task<ContentReportResponse> ResolveAsync(long id, CancellationToken cancellationToken = default) =>
        SetResolutionAsync(id, ContentReportStatus.Resolved, cancellationToken);

    /// <summary>The report itself was invalid or not actionable.</summary>
    public Task<ContentReportResponse> DismissAsync(long id, CancellationToken cancellationToken = default) =>
        SetResolutionAsync(id, ContentReportStatus.Dismissed, cancellationToken);

    /// <summary>Confirms the report and removes the offending content from public view.</summary>
    public async Task<ContentReportResponse> TakeDownAsync(
        long id, string? reason, CancellationToken cancellationToken = default)
    {
        var existing = await reports.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Report {id} was not found.");

        if (existing.Status != ContentReportStatus.Open)
        {
            throw new ConflictException("This report has already been actioned.");
        }

        if (existing.TargetType == ContentTargetType.PartnerProduct)
        {
            await productService.UnpublishAsync(existing.TargetId, currentUser.UserId, reason, cancellationToken);
        }

        return await SetResolutionAsync(existing, ContentReportStatus.Resolved, cancellationToken);
    }

    private async Task<ContentReportResponse> SetResolutionAsync(
        long id, ContentReportStatus status, CancellationToken cancellationToken)
    {
        var existing = await reports.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Report {id} was not found.");

        if (existing.Status != ContentReportStatus.Open)
        {
            throw new ConflictException("This report has already been actioned.");
        }

        return await SetResolutionAsync(existing, status, cancellationToken);
    }

    private async Task<ContentReportResponse> SetResolutionAsync(
        ContentReport existing, ContentReportStatus status, CancellationToken cancellationToken)
    {
        var updated = new ContentReport
        {
            Id = existing.Id,
            ReporterUserId = existing.ReporterUserId,
            TargetType = existing.TargetType,
            TargetId = existing.TargetId,
            Reason = existing.Reason,
            Status = status,
            ResolvedBy = currentUser.UserId,
            ResolvedAtUtc = clock.UtcNow,
            CreatedAtUtc = existing.CreatedAtUtc,
        };

        if (!await reports.UpdateAsync(updated, cancellationToken))
        {
            throw new NotFoundException($"Report {existing.Id} was not found.");
        }

        return ToResponse(updated, existing.Id);
    }

    private static ContentTargetType ParseTargetType(string? targetType)
    {
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            foreach (var candidate in Enum.GetValues<ContentTargetType>())
            {
                if (string.Equals(Naming.ToDbValue(candidate), targetType.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        throw new ValidationException(nameof(ReportContentRequest.TargetType), "Unrecognised content type.");
    }

    private static ContentReportResponse ToResponse(ContentReport report, long id) => new(
        id,
        report.ReporterUserId,
        Naming.ToDbValue(report.TargetType),
        report.TargetId,
        report.Reason,
        Naming.ToDbValue(report.Status),
        report.ResolvedBy,
        report.ResolvedAtUtc,
        report.CreatedAtUtc);

    private static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(nameof(reason), "A reason is required.");
        }

        return reason.Trim();
    }
}
