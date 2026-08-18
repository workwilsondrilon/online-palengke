namespace OnlinePalengke.Application.Moderation;

public sealed record ReportContentRequest(string TargetType, long TargetId, string Reason);

public sealed record ContentReportResponse(
    long Id,
    long ReporterUserId,
    string TargetType,
    long TargetId,
    string Reason,
    string Status,
    long? ResolvedBy,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc);
