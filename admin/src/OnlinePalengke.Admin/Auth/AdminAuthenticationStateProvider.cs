using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace OnlinePalengke.Admin.Auth;

/// <summary>
/// Holds the signed-in admin's session for this circuit and is the single source of truth
/// <see cref="Api.AdminApiClient"/> reads the current access token from.
/// </summary>
/// <remarks>
/// Blazor Server has no per-request cookie the API can read — a signed-in session has to
/// survive circuit reconnects and page reloads some other way, so the token pair is
/// persisted to <see cref="ProtectedSessionStorage"/> (encrypted, tab-scoped browser
/// session storage) on every sign-in and refresh, and re-hydrated from there the first
/// time this provider is asked for auth state on a new circuit. Reading protected browser
/// storage requires JS interop, which is unavailable during static prerendering — Admin's
/// interactive root disables prerendering (see App.razor) specifically so this never has
/// to guess whether interop is ready; the try/catch below is defensive insurance for that
/// invariant, not the primary mechanism.
/// </remarks>
public sealed class AdminAuthenticationStateProvider(ProtectedSessionStorage storage) : AuthenticationStateProvider
{
    private const string StorageKey = "admin-session";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private AdminSessionInfo? _session;
    private bool _hydrated;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureHydratedAsync();
        return new AuthenticationState(_session is null ? Anonymous : BuildPrincipal(_session));
    }

    /// <summary>Returns the current access token, hydrating from storage first if this is a fresh circuit.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        await EnsureHydratedAsync();
        return _session?.AccessToken;
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        await EnsureHydratedAsync();
        return _session?.RefreshToken;
    }

    public async Task SignInAsync(AdminSessionInfo session)
    {
        _session = session;
        _hydrated = true;
        await storage.SetAsync(StorageKey, session);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(session))));
    }

    public async Task SignOutAsync()
    {
        _session = null;
        _hydrated = true;
        await storage.DeleteAsync(StorageKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    /// <summary>Called by <see cref="Api.AdminApiClient"/> after a silent token refresh succeeds.</summary>
    public async Task ReplaceTokensAsync(string accessToken, string refreshToken, DateTime accessTokenExpiresAtUtc)
    {
        if (_session is null)
        {
            return;
        }

        _session = _session with
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
        };

        await storage.SetAsync(StorageKey, _session);
    }

    private async Task EnsureHydratedAsync()
    {
        if (_hydrated)
        {
            return;
        }

        _hydrated = true;

        try
        {
            var result = await storage.GetAsync<AdminSessionInfo>(StorageKey);
            if (result is { Success: true, Value: { } session })
            {
                _session = session;
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop isn't available yet (prerendering). Retry on the next call.
            _hydrated = false;
        }
    }

    private static ClaimsPrincipal BuildPrincipal(AdminSessionInfo session)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Name, session.Email),
            new Claim(ClaimTypes.Role, "admin"),
        ], authenticationType: "AdminSession");

        return new ClaimsPrincipal(identity);
    }
}
