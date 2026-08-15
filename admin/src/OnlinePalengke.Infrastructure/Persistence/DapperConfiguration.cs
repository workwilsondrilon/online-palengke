using Dapper;

namespace OnlinePalengke.Infrastructure.Persistence;

/// <summary>
/// One-time global Dapper setup. Call <see cref="Apply"/> exactly once at startup.
/// </summary>
public static class DapperConfiguration
{
    private static bool _applied;
    private static readonly Lock Gate = new();

    /// <summary>
    /// Configures snake_case column matching for the whole process.
    /// </summary>
    /// <remarks>
    /// Every column in this schema is snake_case and every C# member is PascalCase, so
    /// this single switch removes the need for an alias on almost every SELECT. It is a
    /// static, process-wide setting in Dapper, which is why it is guarded and idempotent
    /// rather than being set from a constructor somewhere.
    /// <para>
    /// Enum columns are deliberately NOT handled by a global type handler. Repositories
    /// map them explicitly through <c>Naming.ToDbValue</c> / <c>Naming.FromDbValue</c> so
    /// that an unrecognised value in the database fails loudly instead of silently
    /// becoming the enum's zero member.
    /// </para>
    /// </remarks>
    public static void Apply()
    {
        lock (Gate)
        {
            if (_applied)
            {
                return;
            }

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            _applied = true;
        }
    }
}
