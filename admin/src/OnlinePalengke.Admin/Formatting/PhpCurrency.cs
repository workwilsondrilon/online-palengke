using System.Globalization;

namespace OnlinePalengke.Admin.Formatting;

/// <summary>
/// The single place in the admin app where a <see cref="decimal"/> becomes peso text.
/// Do not call <c>ToString("C")</c> anywhere else: this solution sets
/// <c>InvariantGlobalization=true</c>, so <c>"C"</c> would render US dollars.
/// Money is always <see cref="decimal"/> — never float or double.
/// </summary>
public static class PhpCurrency
{
    /// <summary>The peso sign, U+20B1.</summary>
    public const string Symbol = "₱";

    /// <summary>ISO 4217 code, used where the bare symbol would be ambiguous.</summary>
    public const string IsoCode = "PHP";

    /// <summary>Renders "₱1,234.50" (and "-₱1,234.50" for negatives).</summary>
    public static string Format(decimal amount) => Format(amount, decimals: 2);

    /// <summary>Renders an amount with an explicit number of decimal places.</summary>
    public static string Format(decimal amount, int decimals)
    {
        var negative = amount < 0m;
        var magnitude = negative ? -amount : amount;
        var digits = magnitude.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        return negative ? $"-{Symbol}{digits}" : $"{Symbol}{digits}";
    }

    /// <summary>Renders a nullable amount, falling back to an em dash.</summary>
    public static string Format(decimal? amount) => amount.HasValue ? Format(amount.Value) : "—";

    /// <summary>Renders "₱1,234.50 PHP" for contexts where the currency must be unambiguous.</summary>
    public static string FormatWithCode(decimal amount) => $"{Format(amount)} {IsoCode}";

    /// <summary>Renders a whole-peso figure without decimals, for headline tiles.</summary>
    public static string FormatCompact(decimal amount)
    {
        var negative = amount < 0m;
        var magnitude = negative ? -amount : amount;

        var text = magnitude switch
        {
            >= 1_000_000m => Symbol + (magnitude / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture) + "M",
            >= 1_000m => Symbol + (magnitude / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "k",
            _ => Format(magnitude, decimals: 0),
        };

        return negative ? $"-{text}" : text;
    }

    /// <summary>Renders a per-unit rate, e.g. "₱12.00 / km".</summary>
    public static string FormatRate(decimal amount, string unit) => $"{Format(amount)} / {unit}";
}
