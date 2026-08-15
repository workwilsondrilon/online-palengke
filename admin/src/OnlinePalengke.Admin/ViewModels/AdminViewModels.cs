namespace OnlinePalengke.Admin.ViewModels;

// Local, presentation-only view models for the admin shell.
//
// These are deliberately NOT domain entities and NOT DTOs shared with the API. They exist
// so the placeholder screens are strongly typed and so the razor markup shows the exact
// shape each screen will need once it is wired to OnlinePalengke.Application. Every
// instant is UTC and named "...Utc"; every money field is decimal.

/// <summary>A headline figure on the dashboard.</summary>
public sealed record StatTileModel(
    string Label,
    string Value,
    string? Caption = null,
    string Tone = "neutral");

/// <summary>Count of today's orders sitting in a given lifecycle status.</summary>
public sealed record OrderStatusCount(string Status, int Count);

/// <summary>A quote window that is currently accepting partner bids.</summary>
public sealed record OpenQuoteWindow(
    string MarketName,
    string WindowLabel,
    DateTimeOffset ClosesAtUtc,
    int ShoppingLists,
    int QuotesReceived);

/// <summary>A rider currently clocked in for a delivery window.</summary>
public sealed record RiderOnShift(
    string RiderName,
    string MarketName,
    string WindowLabel,
    int AssignedStops,
    string Status);

/// <summary>Catalog category. Categories group items and scope the per-category winning bid.</summary>
public sealed record CategoryRow(
    int Id,
    string Name,
    string Slug,
    int ItemCount,
    int SortOrder,
    bool IsActive);

/// <summary>
/// Item master row. The item master intentionally carries no price: prices only ever
/// exist on partner quotes, so no screen in this app may show a price against an item.
/// </summary>
public sealed record ItemRow(
    int Id,
    string Name,
    string CategoryName,
    string DefaultUnitCode,
    decimal WeightPerUnitKg,
    string? ImageUrl,
    bool IsActive);

/// <summary>Unit of measure available to shopping lists and quotes.</summary>
public sealed record UnitRow(
    int Id,
    string Code,
    string Name,
    bool AllowsFractionalQuantity,
    bool IsActive);

/// <summary>A wet market in the network.</summary>
public sealed record MarketRow(
    int Id,
    string Name,
    string City,
    string Province,
    int VerifiedStalls,
    int DeliveryWindows,
    bool HasServiceArea,
    string Status);

/// <summary>A recurring delivery window for one market, expressed in Manila wall-clock time.</summary>
public sealed record DeliveryWindowRow(
    int Id,
    string Label,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    int CutoffOffsetMinutes,
    int Capacity,
    bool IsActive);

/// <summary>A partner or rider document awaiting compliance review.</summary>
public sealed record VerificationRow(
    int Id,
    string SubjectName,
    string SubjectType,
    string DocumentType,
    string MarketName,
    DateTimeOffset SubmittedAtUtc,
    DateOnly? ExpiresOn,
    string Status);

/// <summary>
/// A partner marketing post. Content publishes immediately with no pre-approval, so this
/// row represents something already live and visible to customers.
/// </summary>
public sealed record ContentPostRow(
    int Id,
    string PartnerName,
    string MarketName,
    string Headline,
    string Copy,
    DateTimeOffset PublishedAtUtc,
    int Reports,
    string Status);

/// <summary>A customer order in the operations list.</summary>
public sealed record OrderRow(
    string Reference,
    string CustomerName,
    string MarketName,
    string WindowLabel,
    DateOnly DeliveryDate,
    DateTimeOffset PlacedAtUtc,
    int LineCount,
    decimal Total,
    string Status);

/// <summary>One purchased line on an order, priced from the winning partner quote.</summary>
public sealed record OrderLineRow(
    string ItemName,
    string CategoryName,
    decimal Quantity,
    string UnitCode,
    string WinningStall,
    decimal QuotedUnitPrice,
    decimal LineTotal);

/// <summary>A batched delivery run: one market, one window, one date, one rider.</summary>
public sealed record DeliveryRunRow(
    string Reference,
    string MarketName,
    string WindowLabel,
    DateOnly RunDate,
    int Stops,
    decimal TotalWeightKg,
    string? AssignedRider,
    string Status);

/// <summary>Money owed to a partner or rider, awaiting a payout batch.</summary>
public sealed record PayoutBalanceRow(
    int Id,
    string PayeeName,
    string PayeeType,
    string MarketName,
    int UnsettledOrders,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetPayable,
    DateTimeOffset? OldestUnpaidUtc);

/// <summary>A generated payout batch.</summary>
public sealed record PayoutBatchRow(
    string Reference,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Payees,
    decimal TotalAmount,
    string? PaymentReference,
    DateTimeOffset? PaidAtUtc,
    string Status);

/// <summary>One entry in the audit trail.</summary>
public sealed record AuditEntryRow(
    long Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string ActorRole,
    string Action,
    string EntityType,
    string EntityReference,
    string Summary);

/// <summary>A selectable filter chip. <see cref="Count"/> is null when unknown.</summary>
public sealed record FilterChip(string Key, string Label, int? Count = null);

/// <summary>
/// Editable platform configuration. Mutable class rather than a record because it is bound
/// to form inputs. Values here are placeholders until the Application layer supplies them.
/// </summary>
public sealed class PlatformSettings
{
    // Delivery fee parameters.
    public decimal BaseFare { get; set; } = 49.00m;
    public decimal BaseKm { get; set; } = 3.0m;
    public decimal PerKm { get; set; } = 12.00m;
    public decimal WeightThresholdKg { get; set; } = 10.0m;
    public decimal PerKgOverThreshold { get; set; } = 4.50m;
    public decimal RoadFactor { get; set; } = 1.35m;

    // Commission.
    public decimal PartnerCommissionPercent { get; set; } = 8.0m;
    public decimal RiderCommissionPercent { get; set; } = 15.0m;

    // Quoting.
    public int QuoteWindowMinutes { get; set; } = 45;
    public int QuoteExpiryMinutes { get; set; } = 120;
}
