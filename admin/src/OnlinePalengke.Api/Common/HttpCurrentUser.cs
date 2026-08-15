using System.Security.Claims;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Api.Common;

/// <summary>Resolves <see cref="ICurrentUser"/> from the authenticated principal.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Claim carrying the user's database id.</summary>
    public const string UserIdClaim = "sub";

    /// <summary>Claim carrying the user's role, as a snake_case string.</summary>
    public const string RoleClaim = "role";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(UserIdClaim);

            return long.TryParse(value, out var id)
                ? id
                : throw new InvalidOperationException(
                    "The request has no authenticated user. Check IsAuthenticated before reading UserId.");
        }
    }

    public UserRole Role
    {
        get
        {
            var value = Principal?.FindFirstValue(RoleClaim)
                ?? throw new InvalidOperationException(
                    "The request has no authenticated user. Check IsAuthenticated before reading Role.");

            return Naming.FromDbValue<UserRole>(value);
        }
    }
}
