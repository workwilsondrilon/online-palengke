using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OnlinePalengke.Api.Common;
using OnlinePalengke.Api.Endpoints;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Infrastructure.Auth;

namespace OnlinePalengke.Api.Configuration;

/// <summary>Authentication, authorization and route-group wiring for the API.</summary>
public static class ApiSetup
{
    /// <summary>The authorization policy name for a role, e.g. <c>role:partner</c>.</summary>
    public static string PolicyFor(UserRole role) => $"role:{Naming.ToDbValue(role)}";

    /// <summary>Registers JWT Bearer as the API's sole authentication mechanism.</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException(
                        $"The '{JwtOptions.SectionName}' configuration section is missing.");

                // JwtBearerHandler otherwise silently remaps short claim types it
                // recognises - "role" chief among them - to their long legacy
                // ClaimTypes.* URIs on the validated principal. Without this, the
                // short "role" claim HttpCurrentUser and every authorization policy
                // read simply stops existing after validation, and every request
                // fails authorization with an empty 403 despite a perfectly valid
                // token. Keep claims exactly as issued.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    // Short-lived (15 min) access tokens don't need the 5-minute default
                    // slack — tighten it so expiry actually means what it says.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

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
    /// Maps the four role-scoped route groups, plus the anonymous auth endpoints that
    /// live inside them (and the two that don't belong to any single role).
    /// </summary>
    /// <remarks>
    /// Each mobile app and the admin web app talk to exactly one group. The group policy
    /// is the coarse gate; finer checks — which purpose a role may upload for, whether a
    /// partner is verified — live in the application layer where they can be unit-tested.
    /// Auth endpoints inside a group are individually <c>AllowAnonymous</c>, which
    /// overrides the group's <c>RequireAuthorization</c> for just that endpoint — nobody
    /// calling <c>/otp/request</c> has a token yet.
    /// </remarks>
    public static void MapApiGroups(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGroup("/customer")
            .RequireAuthorization(PolicyFor(UserRole.Customer))
            .MapOtpEndpoints(UserRole.Customer, "customer")
            .MapUploadEndpoints("customer");

        api.MapGroup("/partner")
            .RequireAuthorization(PolicyFor(UserRole.Partner))
            .MapOtpEndpoints(UserRole.Partner, "partner")
            .MapUploadEndpoints("partner");

        api.MapGroup("/rider")
            .RequireAuthorization(PolicyFor(UserRole.Rider))
            .MapOtpEndpoints(UserRole.Rider, "rider")
            .MapUploadEndpoints("rider");

        var admin = api.MapGroup("/admin")
            .RequireAuthorization(PolicyFor(UserRole.Admin));
        admin.MapAdminLoginEndpoint();
        admin.MapUploadEndpoints("admin");

        api.MapGlobalAuthEndpoints();
    }
}
