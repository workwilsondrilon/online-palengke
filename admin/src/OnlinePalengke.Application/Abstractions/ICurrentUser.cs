using OnlinePalengke.Domain.Identity;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>
/// Who is making the current request, resolved from the authenticated principal.
/// </summary>
/// <remarks>
/// Application services take this rather than reading claims directly, so that
/// authorization decisions are expressed against the domain's own vocabulary and can
/// be unit-tested without constructing an HTTP context.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>True when the request carries an authenticated principal.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The signed-in user's id. Throws when unauthenticated — call sites that can be
    /// reached anonymously must check <see cref="IsAuthenticated"/> first.
    /// </summary>
    long UserId { get; }

    /// <summary>The signed-in user's role. Throws when unauthenticated.</summary>
    UserRole Role { get; }
}
