namespace OnlinePalengke.Domain.Moderation;

/// <summary>Review state of a content report.</summary>
public enum ContentReportStatus
{
    /// <summary>Reported, awaiting an admin reviewer.</summary>
    Open,

    /// <summary>Reviewed and actioned (e.g. the product was taken down).</summary>
    Resolved,

    /// <summary>Reviewed and found to need no action.</summary>
    Dismissed,
}
