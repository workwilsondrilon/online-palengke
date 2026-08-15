using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OnlinePalengke.Api.Common;
using OnlinePalengke.Api.Configuration;
using OnlinePalengke.Api.Health;
using OnlinePalengke.Application;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Infrastructure;
using Serilog;
using Serilog.Events;

// Bootstrap logger, so a failure during host construction is still recorded somewhere
// rather than vanishing into a silent non-start.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    builder.Services.AddOpenApi();

    builder.Services.AddApiAuthentication(builder.Environment, builder.Configuration);

    var app = builder.Build();

    if (ApiSetup.IsDevHeaderAuthEnabled(app.Environment, app.Configuration))
    {
        Log.Warning(
            "DEVELOPMENT HEADER AUTHENTICATION IS ENABLED. Any caller can assume any identity by "
            + "sending X-Dev-User-Id and X-Dev-Role. This must never be enabled outside local development.");
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness: the process is up. Deliberately does not touch the database, so that a
    // database outage does not cause an orchestrator to restart otherwise-healthy instances.
    app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
        .AllowAnonymous();

    // Readiness: the process can actually serve traffic, database included.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    }).AllowAnonymous();

    app.MapApiGroups();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The API terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Exposed so <c>WebApplicationFactory</c> in the integration tests can locate the entry
/// point of this top-level-statements program.
/// </summary>
public partial class Program;
