using Microsoft.AspNetCore.Authentication;
using OnlinePalengke.Api.Authentication;
using OnlinePalengke.Api.Common;
using OnlinePalengke.Api.Endpoints;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Api.Configuration;

/// <summary>Authentication, authorization and route-group wiring for the API.</summary>
public static class ApiSetup
{
    /// <summary>The authorization policy name for a role, e.g. <c>role:partner</c>.</summary>
    public static string PolicyFor(UserRole role) => $"role:{Naming.ToDbValue(role)}";

    /// <summary>
    /// Whether the development header authentication scheme is active.
    /// </summary>
    /// <remarks>
    /// Requires the Development environment <i>and</i> an explicit configuration opt-in.
    /// Two independent conditions, so that neither a mis-set environment variable nor a
    /// stray config value is sufficient on its own to expose it.
    /// </remarks>
    public static bool IsDevHeaderAuthEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        && configuration.GetValue<bool>("Auth:EnableDevHeaderScheme");

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var authentication = services.AddAuthentication(DevHeaderAuthenticationHandler.SchemeName);

        // Real phone-OTP JWT authentication arrives in the next milestone. Until then the
        // dev header scheme is the only way to present an identity, and when it is off
        // every protected endpoint correctly returns 401.
        if (IsDevHeaderAuthEnabled(environment, configuration))
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(
                DevHeaderAuthenticationHandler.SchemeName, _ => { });
        }
        else
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DenyAllAuthenticationHandler>(
                DevHeaderAuthenticationHandler.SchemeName, _ => { });
        }

        var authorization = services.AddAuthorizationBuilder();

        foreach (var role in Enum.GetValues<UserRole>())
        {
            authorization.AddPolicy(
                PolicyFor(role),
                policy => policy.RequireClaim(HttpCurrentUser.RoleClaim, Naming.ToDbValue(role)));
        }

        return services;
    }

    /// <summary>
    /// Maps the four role-scoped route groups.
    /// </summary>
    /// <remarks>
    /// Each mobile app and the admin web app talk to exactly one group. The group policy
    /// is the coarse gate; finer checks — which purpose a role may upload for, whether a
    /// partner is verified — live in the application layer where they can be unit-tested.
    /// </remarks>
    public static void MapApiGroups(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGroup("/customer")
            .RequireAuthorization(PolicyFor(UserRole.Customer))
            .MapUploadEndpoints("customer");

        api.MapGroup("/partner")
            .RequireAuthorization(PolicyFor(UserRole.Partner))
            .MapUploadEndpoints("partner");

        api.MapGroup("/rider")
            .RequireAuthorization(PolicyFor(UserRole.Rider))
            .MapUploadEndpoints("rider");

        api.MapGroup("/admin")
            .RequireAuthorization(PolicyFor(UserRole.Admin))
            .MapUploadEndpoints("admin");
    }
}
