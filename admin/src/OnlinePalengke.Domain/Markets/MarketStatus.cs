namespace OnlinePalengke.Domain.Markets;

/// <summary>
/// Operational state of a market. Deliberately three states rather than an
/// <c>is_active</c> boolean: a market being set up and a market pulled offline for cause
/// are different situations, and the admin list needs to tell them apart at a glance.
/// </summary>
/// <remarks>
/// Persisted as snake_case strings in <c>markets.status</c>, guarded by a CHECK
/// constraint — same convention as <c>users.status</c> and <c>partners.status</c>.
/// </remarks>
public enum MarketStatus
{
    /// <summary>
    /// Being set up. Stalls may be recruited and verified, but the market is invisible to
    /// customers and its polygon makes no address eligible — a service area is usually not
    /// drawn yet at this point.
    /// </summary>
    Onboarding,

    /// <summary>Live: customers inside the service area can order from it.</summary>
    Active,

    /// <summary>
    /// Pulled offline by an operator. Distinct from <see cref="Onboarding"/> because the
    /// market was once live — orders already placed against it still exist and must remain
    /// resolvable, so suspension never deletes anything.
    /// </summary>
    Suspended,
}
