namespace OnlinePalengke.Domain.Markets;

/// <summary>
/// A recurring delivery slot for one market — "7–9 AM", every day. Orders batch by market
/// plus window plus date, so this is what a customer picks at checkout and what a delivery
/// run is assembled from (decision 13).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StartsAt"/> and <see cref="EndsAt"/> are the one deliberate exception to the
/// "everything is UTC" rule in this codebase, and they are <see cref="TimeOnly"/> rather
/// than <see cref="DateTime"/> to make that impossible to misread. A window is a recurring
/// Asia/Manila wall-clock schedule, not an instant: "7 AM" must stay 7 AM to the customer
/// and to the stall regardless of anything else. Converting to UTC here would produce
/// 23:00 on the previous day, which no operator editing this screen would recognise.
/// The conversion to a real instant happens per delivery date, in the Application layer.
/// </para>
/// <para>
/// A window never crosses midnight — <see cref="EndsAt"/> is always later in the same day
/// than <see cref="StartsAt"/>. Nothing in the platform needs an overnight slot, and
/// allowing one would make every "is this window open" comparison a two-case check.
/// </para>
/// </remarks>
public sealed class DeliveryWindow
{
    public long Id { get; init; }

    public required long MarketId { get; init; }

    /// <summary>
    /// Human label shown to customers, partners and riders alike — "7–9 AM". Stored rather
    /// than formatted from the times so an operator can write "Early morning" or a Filipino
    /// label if that reads better locally.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>Manila wall-clock opening time. See the remarks on this type.</summary>
    public required TimeOnly StartsAt { get; init; }

    /// <summary>Manila wall-clock closing time, always later the same day than <see cref="StartsAt"/>.</summary>
    public required TimeOnly EndsAt { get; init; }

    /// <summary>
    /// How many minutes before <see cref="StartsAt"/> that ordering — and therefore
    /// quoting — closes. It has to be long enough for the whole RFQ round to run: broadcast,
    /// partners quote, customer picks, customer pays, stalls pick. 90 minutes is the typical
    /// setting; anything shorter and a quote window (decision 25) cannot finish in time.
    /// </summary>
    public required int CutoffOffsetMinutes { get; init; }

    /// <summary>
    /// Maximum orders accepted into this slot per date, bounded by how many stops the
    /// available riders can actually cover in a two-hour run.
    /// </summary>
    public required int Capacity { get; init; }

    /// <summary>
    /// Deactivating a window stops new bookings without deleting it — orders already placed
    /// inside it still reference this row and must keep resolving.
    /// </summary>
    public required bool IsActive { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
