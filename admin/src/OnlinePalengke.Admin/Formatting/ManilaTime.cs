using System.Globalization;

namespace OnlinePalengke.Admin.Formatting;

/// <summary>
/// The single place in the admin app where UTC instants become Asia/Manila wall-clock
/// text. Every timestamp that reaches a razor page must pass through here.
/// </summary>
/// <remarks>
/// Two environment quirks are handled in <see cref="ResolveZone"/>:
/// <list type="bullet">
/// <item>Windows only resolves IANA ids such as "Asia/Manila" through ICU. This solution
/// sets <c>InvariantGlobalization=true</c> in Directory.Build.props, which can strip ICU
/// out of the runtime, so the IANA lookup may throw.</item>
/// <item>The Windows registry id that covers Manila is "Singapore Standard Time".</item>
/// </list>
/// If both lookups fail we fall back to a fixed UTC+08:00 zone. The Philippines has not
/// observed DST since 1978, so a fixed offset is correct rather than merely convenient.
/// Change <see cref="ResolveZone"/> and the whole app follows.
/// </remarks>
public static class ManilaTime
{
    /// <summary>Short label appended to rendered timestamps.</summary>
    public const string Abbreviation = "PHT";

    private const string DefaultDateTimeFormat = "dd MMM yyyy, h:mm tt";
    private const string DefaultDateFormat = "dd MMM yyyy";
    private const string DefaultTimeFormat = "h:mm tt";

    private static readonly TimeZoneInfo ManilaZone = ResolveZone();

    /// <summary>The resolved Philippine time zone.</summary>
    public static TimeZoneInfo Zone => ManilaZone;

    /// <summary>Human readable description of how the zone was resolved, for diagnostics.</summary>
    public static string ZoneDescription => $"{ManilaZone.Id} (UTC{ManilaZone.BaseUtcOffset.Hours:+00;-00}:{Math.Abs(ManilaZone.BaseUtcOffset.Minutes):00})";

    /// <summary>Current instant expressed in Manila local time.</summary>
    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ManilaZone);

    /// <summary>Today's date in Manila.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>Converts an instant to Manila local time.</summary>
    public static DateTimeOffset ToManila(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, ManilaZone);

    /// <summary>
    /// Converts a <see cref="DateTime"/> to Manila local time. Unspecified kinds are
    /// treated as UTC, because everything crossing our API boundary is stored UTC.
    /// </summary>
    public static DateTimeOffset ToManila(DateTime instant)
    {
        var utc = instant.Kind switch
        {
            DateTimeKind.Utc => instant,
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instant, DateTimeKind.Utc),
        };

        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), ManilaZone);
    }

    /// <summary>Renders date and time, e.g. "14 Aug 2026, 7:05 AM".</summary>
    public static string Format(DateTimeOffset instant) =>
        ToManila(instant).ToString(DefaultDateTimeFormat, CultureInfo.InvariantCulture);

    /// <inheritdoc cref="Format(DateTimeOffset)"/>
    public static string Format(DateTime instant) =>
        ToManila(instant).ToString(DefaultDateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Renders date and time with the "PHT" suffix.</summary>
    public static string FormatWithZone(DateTimeOffset instant) =>
        $"{Format(instant)} {Abbreviation}";

    /// <summary>Renders the date only, e.g. "14 Aug 2026".</summary>
    public static string FormatDate(DateTimeOffset instant) =>
        ToManila(instant).ToString(DefaultDateFormat, CultureInfo.InvariantCulture);

    /// <summary>Renders the time only, e.g. "7:05 AM".</summary>
    public static string FormatTime(DateTimeOffset instant) =>
        ToManila(instant).ToString(DefaultTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Renders a Manila wall-clock time of day, e.g. "7:00 AM".</summary>
    public static string FormatTimeOfDay(TimeOnly timeOfDay) =>
        timeOfDay.ToString(DefaultTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Renders a plain date, e.g. "14 Aug 2026".</summary>
    public static string FormatDate(DateOnly date) =>
        date.ToString(DefaultDateFormat, CultureInfo.InvariantCulture);

    /// <summary>Machine readable value for the <c>datetime</c> attribute of &lt;time&gt;.</summary>
    public static string ToIso8601(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Coarse "3 h ago" style label. Good enough for queue and audit screens; deliberately
    /// not localised because the admin locale is English.
    /// </summary>
    public static string Relative(DateTimeOffset instant)
    {
        var delta = DateTimeOffset.UtcNow - instant.ToUniversalTime();
        var future = delta < TimeSpan.Zero;
        var magnitude = future ? delta.Negate() : delta;

        var text = magnitude.TotalSeconds switch
        {
            < 60 => "moments",
            < 3600 => $"{(int)magnitude.TotalMinutes} min",
            < 86400 => $"{(int)magnitude.TotalHours} h",
            < 2592000 => $"{(int)magnitude.TotalDays} d",
            _ => $"{(int)(magnitude.TotalDays / 30)} mo",
        };

        return future ? $"in {text}" : $"{text} ago";
    }

    private static TimeZoneInfo ResolveZone()
    {
        string[] candidateIds = ["Asia/Manila", "Singapore Standard Time", "Taipei Standard Time"];

        foreach (var id in candidateIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next candidate.
            }
            catch (InvalidTimeZoneException)
            {
                // Corrupt zone data; try the next candidate.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "Asia/Manila",
            baseUtcOffset: TimeSpan.FromHours(8),
            displayName: "(UTC+08:00) Manila",
            standardDisplayName: "Philippine Standard Time");
    }
}
