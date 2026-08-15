using System.ComponentModel.DataAnnotations;

namespace OnlinePalengke.Infrastructure.Persistence;

/// <summary>Database configuration, bound from the <c>Database</c> configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// MySQL connection string. Never commit a real one — use user-secrets locally and
    /// the host's secret store in deployed environments.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Seconds a command may run before it is cancelled.</summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}
