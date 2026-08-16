namespace OnlinePalengke.Domain.Kyc;

/// <summary>Review state of one KYC document submission.</summary>
/// <remarks>
/// Persisted as a snake_case string in <c>kyc_documents.status</c>, guarded by a CHECK
/// constraint — same convention as <c>partners.status</c>/<c>riders.status</c>.
/// </remarks>
public enum KycDocumentStatus
{
    /// <summary>Uploaded, awaiting an admin reviewer.</summary>
    Submitted,

    /// <summary>Reviewed and accepted. Contributes toward the owner's verified status while unexpired.</summary>
    Approved,

    /// <summary>Reviewed and declined, with a reason. The owner resubmits by uploading a new document.</summary>
    Rejected,

    /// <summary>Was approved, but its <c>ExpiresAt</c> has lapsed. Set by the daily expiry job.</summary>
    Expired,
}
