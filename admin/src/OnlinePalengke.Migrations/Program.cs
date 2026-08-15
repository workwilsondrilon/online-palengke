using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace OnlinePalengke.Migrations;

/// <summary>
/// Applies the SQL scripts embedded under <c>Scripts/</c> to a MySQL database using DbUp.
/// Scripts are applied once, in ordinal name order, and recorded in the journal table so
/// re-running the tool is a no-op. Designed to be safe to run unattended in CI.
/// </summary>
internal static class Program
{
    /// <summary>Environment variable checked when no <c>--connection</c> argument is supplied.</summary>
    private const string ConnectionEnvironmentVariable = "PALENGKE_DB_CONNECTION";

    /// <summary>Key read from appsettings.json as the last-resort source of the connection string.</summary>
    private const string ConnectionStringName = "Palengke";

    /// <summary>DbUp's journal table. snake_case, like every other table in this schema.</summary>
    private const string JournalTable = "schema_versions";

    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitUsageError = 2;

    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            PrintUsage();
            return ExitUsageError;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return ExitSuccess;
        }

        var connectionString = ResolveConnectionString(options.ConnectionString, out var source);
        if (connectionString is null)
        {
            Console.Error.WriteLine(
                "ERROR: no database connection string was found. Supply one of the following, in this order of precedence:\n" +
                "  1. --connection \"Server=127.0.0.1;Port=3306;Database=onlinepalengke;User ID=...;Password=...\"\n" +
                $"  2. the {ConnectionEnvironmentVariable} environment variable\n" +
                $"  3. a \"ConnectionStrings:{ConnectionStringName}\" entry in appsettings.json next to the executable");
            return ExitUsageError;
        }

        MySqlConnectionStringBuilder builder;
        try
        {
            builder = new MySqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"ERROR: the connection string from {source} is not valid: {ex.Message}");
            return ExitUsageError;
        }

        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            Console.Error.WriteLine(
                $"ERROR: the connection string from {source} does not name a database. Add 'Database=onlinepalengke;'.");
            return ExitUsageError;
        }

        Console.WriteLine($"Online Palengke migrations (DbUp {DbUpVersion()})");
        Console.WriteLine($"  connection source : {source}");
        Console.WriteLine($"  target            : {Describe(builder)}");
        Console.WriteLine($"  journal table     : {JournalTable}");
        Console.WriteLine($"  mode              : {(options.DryRun ? "dry run (nothing will be applied)" : "apply")}");
        Console.WriteLine();

        try
        {
            if (options.EnsureDatabase)
            {
                if (options.DryRun)
                {
                    Console.WriteLine("--ensure-database is ignored during --dry-run; the database must already exist.");
                }
                else
                {
                    Console.WriteLine($"Ensuring database '{builder.Database}' exists...");
                    EnsureDatabase.For.MySqlDatabase(connectionString);
                }
            }

            var engine = BuildEngine(connectionString, builder.Database);

            if (!engine.TryConnect(out var connectionError))
            {
                Console.Error.WriteLine($"ERROR: could not connect to the database: {connectionError}");
                Console.Error.WriteLine("If the database has not been created yet, re-run with --ensure-database.");
                return ExitFailure;
            }

            return options.DryRun ? ReportPendingScripts(engine) : ApplyPendingScripts(engine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine(ex);
            return ExitFailure;
        }
    }

    private static UpgradeEngine BuildEngine(string connectionString, string database) =>
        DeployChanges.To
            .MySqlDatabase(connectionString)
            // Scripts are embedded resources; the filter keeps us to our own Scripts folder
            // so nothing another package embeds can ever be mistaken for a migration.
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.Contains(".Scripts.", StringComparison.Ordinal) &&
                        name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            // The database name must be passed as the journal "schema". DbUp's MySQL
            // journal probes information_schema.tables for the journal table, and with
            // no schema it omits the table_schema predicate -- so a schema_versions
            // table in any other database on the same server makes it wrongly conclude
            // the journal already exists here, and the run then fails reading it.
            .JournalToMySqlTable(database, JournalTable)
            // MySQL DDL is not transactional (every CREATE TABLE implicitly commits), so
            // wrapping the run in a transaction would give a false sense of atomicity.
            // Each script must therefore be independently safe to re-run after a failure.
            .LogToConsole()
            .Build();

    private static int ReportPendingScripts(UpgradeEngine engine)
    {
        var pending = engine.GetScriptsToExecute();
        if (pending.Count == 0)
        {
            Console.WriteLine("No pending scripts. The database is up to date.");
            return ExitSuccess;
        }

        Console.WriteLine($"{pending.Count} pending script(s) would be applied, in this order:");
        for (var i = 0; i < pending.Count; i++)
        {
            Console.WriteLine($"  {i + 1,3}. {pending[i].Name}");
        }

        Console.WriteLine();
        Console.WriteLine("Dry run complete; nothing was applied.");
        return ExitSuccess;
    }

    private static int ApplyPendingScripts(UpgradeEngine engine)
    {
        var pending = engine.GetScriptsToExecute();
        if (pending.Count == 0)
        {
            Console.WriteLine("No pending scripts. The database is up to date.");
            return ExitSuccess;
        }

        Console.WriteLine($"{pending.Count} pending script(s) to apply.");
        var result = engine.PerformUpgrade();

        if (!result.Successful)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"MIGRATION FAILED on script '{result.ErrorScript?.Name ?? "(unknown)"}'.");
            if (result.Error is not null)
            {
                Console.Error.WriteLine(result.Error.Message);
            }

            return ExitFailure;
        }

        var applied = result.Scripts.ToList();
        Console.WriteLine();
        Console.WriteLine($"Applied {applied.Count} script(s):");
        foreach (var script in applied)
        {
            Console.WriteLine($"  + {script.Name}");
        }

        Console.WriteLine();
        Console.WriteLine("Migration succeeded.");
        return ExitSuccess;
    }

    /// <summary>
    /// Resolves the connection string in the documented precedence order:
    /// CLI argument, then environment variable, then appsettings.json.
    /// </summary>
    private static string? ResolveConnectionString(string? fromCommandLine, out string source)
    {
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
        {
            source = "--connection argument";
            return fromCommandLine;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            source = $"{ConnectionEnvironmentVariable} environment variable";
            return fromEnvironment;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var fromConfiguration = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(fromConfiguration))
        {
            source = $"appsettings.json (ConnectionStrings:{ConnectionStringName})";
            return fromConfiguration;
        }

        source = "none";
        return null;
    }

    /// <summary>Renders the connection target without ever echoing the password.</summary>
    private static string Describe(MySqlConnectionStringBuilder builder) =>
        $"{builder.Server}:{builder.Port}/{builder.Database} as '{builder.UserID}'";

    private static string DbUpVersion() =>
        typeof(DeployChanges).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Online Palengke database migration runner

            Usage:
              OnlinePalengke.Migrations [options]

            Options:
              --connection <value>   Connection string to migrate. Takes precedence over
                                     the PALENGKE_DB_CONNECTION environment variable, which
                                     in turn takes precedence over appsettings.json.
              --ensure-database      Create the target database if it does not exist yet.
              --dry-run              List the scripts that would be applied, apply nothing.
              -h, --help             Show this help.

            Exit codes:
              0  success (including "nothing to do")
              1  migration or connection failure
              2  bad arguments or no connection string found
            """);
    }

    /// <summary>Parsed command line. Accepts both '--opt value' and '--opt=value'.</summary>
    private sealed record Options(string? ConnectionString, bool DryRun, bool EnsureDatabase, bool ShowHelp)
    {
        public static Options Parse(string[] args)
        {
            string? connectionString = null;
            var dryRun = false;
            var ensureDatabase = false;
            var showHelp = false;

            for (var i = 0; i < args.Length; i++)
            {
                var (name, inlineValue) = SplitArgument(args[i]);

                switch (name)
                {
                    case "--connection":
                        connectionString = inlineValue ?? TakeNext(args, ref i, "--connection");
                        break;
                    case "--dry-run":
                        dryRun = true;
                        break;
                    case "--ensure-database":
                        ensureDatabase = true;
                        break;
                    case "-h":
                    case "--help":
                        showHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"unrecognised argument '{args[i]}'.");
                }
            }

            return new Options(connectionString, dryRun, ensureDatabase, showHelp);
        }

        private static (string Name, string? Value) SplitArgument(string argument)
        {
            var separator = argument.IndexOf('=', StringComparison.Ordinal);
            return separator < 0
                ? (argument, null)
                : (argument[..separator], argument[(separator + 1)..]);
        }

        private static string TakeNext(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{option} requires a value.");
            }

            index++;
            return args[index];
        }
    }
}
