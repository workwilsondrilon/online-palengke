namespace OnlinePalengke.Domain.Moderation;

/// <summary>A flag raised against a published piece of content, for an admin to review.</summary>
public sealed class ContentReport
{
    public long Id { get; init; }

    public required long ReporterUserId { get; init; }

    public required ContentTargetType TargetType { get; init; }

    public required long TargetId { get; init; }

    public required string Reason { get; init; }

    public required ContentReportStatus Status { get; init; }

    public long? ResolvedBy { get; init; }

    public DateTime? ResolvedAtUtc { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
