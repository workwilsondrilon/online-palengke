using System.Text.RegularExpressions;

namespace OnlinePalengke.Domain.Identity;

/// <summary>
/// Normalizes and validates Philippine mobile numbers to E.164.
/// </summary>
/// <remarks>
/// Mirrors <c>PhoneNumberPh</c> in the Flutter shared package. Client-side
/// normalization is a UX convenience only — the server re-validates
/// independently and never trusts a client-supplied phone string as already
/// being in E.164 shape, since the OTP row is keyed on this exact value.
/// </remarks>
public static partial class PhilippineMobileNumber
{
    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    /// <summary>
    /// Normalizes shapes users actually type ("0917 123 4567", "+63 917 123 4567",
    /// "63 917 123 4567", "9171234567") into E.164 "+639171234567". Returns null
    /// when the input cannot be a PH mobile number.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = NonDigits().Replace(input, string.Empty);

        if (digits.StartsWith("63", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        // PH mobile subscriber numbers are 10 digits and always begin with 9.
        return digits.Length == 10 && digits.StartsWith('9')
            ? $"+63{digits}"
            : null;
    }

    public static bool IsValid(string? input) => Normalize(input) is not null;
}
