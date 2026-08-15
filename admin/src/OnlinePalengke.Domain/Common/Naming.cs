using System.Text;

namespace OnlinePalengke.Domain.Common;

/// <summary>
/// Converts between the PascalCase used in C# and the snake_case used everywhere in
/// MySQL — both for column names and for the string values stored in the VARCHAR
/// columns that stand in for enums.
/// </summary>
/// <remarks>
/// This lives in Domain rather than Infrastructure because the enum-to-string
/// contract it defines is part of the persisted data format, not a storage detail.
/// Changing it changes what is written to the database.
/// </remarks>
public static class Naming
{
    /// <summary>
    /// "PendingKyc" -> "pending_kyc", "CreatedAt" -> "created_at", "SizeBytes" -> "size_bytes".
    /// Runs of capitals are kept together: "KycDocument" -> "kyc_document".
    /// </summary>
    public static string ToSnakeCase(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var builder = new StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (char.IsUpper(current))
            {
                var isStart = i == 0;
                var previousIsLower = !isStart && char.IsLower(value[i - 1]);
                var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                var previousIsUpper = !isStart && char.IsUpper(value[i - 1]);

                // Break before a capital that starts a new word: either the previous
                // character was lowercase ("createdAt"), or we are at the end of a
                // run of capitals that is starting a new word ("HTTPServer" -> http_server).
                if (!isStart && (previousIsLower || (previousIsUpper && nextIsLower)))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else if (current == '_' || current == '-' || current == ' ')
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }
            }
            else
            {
                builder.Append(char.ToLowerInvariant(current));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Compares a database column name against a C# member name, ignoring the
    /// snake_case/PascalCase difference. Used by the Dapper column mapper.
    /// </summary>
    public static bool ColumnMatchesMember(string columnName, string memberName) =>
        string.Equals(columnName, memberName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(columnName, ToSnakeCase(memberName), StringComparison.OrdinalIgnoreCase);

    /// <summary>Formats an enum member as the snake_case string stored in the database.</summary>
    public static string ToDbValue<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        ToSnakeCase(value.ToString()!);

    /// <summary>
    /// Parses a snake_case database value back to its enum member.
    /// </summary>
    /// <remarks>
    /// Throws rather than falling back to a default. A value in the database that this
    /// build does not recognise means the schema and the code have diverged, and quietly
    /// coercing it to the zero member would corrupt behaviour in a way that is very hard
    /// to trace — for a status column, it would silently treat an unknown state as the
    /// first one, which for <c>PartnerStatus</c> would be "pending_kyc".
    /// </remarks>
    public static TEnum FromDbValue<TEnum>(string value)
        where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(ToDbValue(candidate), value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"'{value}' is not a recognised {typeof(TEnum).Name}. " +
            "The database contains a value this build does not know about — check for a missing migration.");
    }
}
