using Microsoft.Extensions.Diagnostics.HealthChecks;
using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Api.Health;

/// <summary>Confirms the API can open a connection to MySQL and run a trivial query.</summary>
/// <remarks>
/// Opens its own connection through the factory rather than using the request-scoped
/// session, so that a health probe never joins an ambient transaction or holds a
/// connection that application work is waiting on.
/// </remarks>
public sealed class DatabaseHealthCheck(IDbConnectionFactory factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = factory.Create();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}
