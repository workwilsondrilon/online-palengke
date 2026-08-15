using System.Data.Common;
using Microsoft.Extensions.Options;
using MySqlConnector;
using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure.Persistence;

/// <summary>Creates unopened MySQL connections from the configured connection string.</summary>
public sealed class MySqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    private readonly DatabaseOptions _options = options.Value;

    public DbConnection Create() => new MySqlConnection(_options.ConnectionString);
}
