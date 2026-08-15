using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OnlinePalengke.Admin.Auth;

namespace OnlinePalengke.Admin.Api;

/// <summary>
/// The Admin web app's sole path to the backend — a typed HTTP client mirroring
/// <c>palengke_core</c>'s <c>ApiClient</c> on the Flutter side (base URL, bearer
/// attachment, typed errors from ProblemDetails), reused by every later admin epic.
/// </summary>
/// <remarks>
/// Every call except <see cref="LoginAsync"/> attaches the current access token from
/// <see cref="AdminAuthenticationStateProvider"/> and, on a single 401, attempts one
/// silent refresh via <c>/api/auth/refresh</c> before retrying — mirroring the mobile
/// apps' <c>AuthInterceptor</c>. A second 401 after that retry is a real "please sign
/// in again", not a token that just needed renewing.
/// </remarks>
public sealed class AdminApiClient(HttpClient http, AdminAuthenticationStateProvider authState)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminLoginResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/admin/auth/login", new AdminLoginRequest(email, password), JsonOptions, cancellationToken);

        return await ReadOrThrowAsync<AdminLoginResponse>(response, cancellationToken);
    }

    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        RequestAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    public Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
        RequestAsync<T>(HttpMethod.Post, path, body, cancellationToken);

    public Task<T> PutAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
        RequestAsync<T>(HttpMethod.Put, path, body, cancellationToken);

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Delete, path, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }
    }

    private async Task<T> RequestAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedAsync(method, path, body, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var token = await authState.GetAccessTokenAsync();
        var response = await SendOnceAsync(method, path, body, token, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(cancellationToken))
        {
            response.Dispose();
            token = await authState.GetAccessTokenAsync();
            response = await SendOnceAsync(method, path, body, token, cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method, string path, object? body, string? accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await http.SendAsync(request, cancellationToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await authState.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        using var response = await http.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(refreshToken), JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var refreshed = await response.Content.ReadFromJsonAsync<RefreshedSession>(JsonOptions, cancellationToken);
        if (refreshed is null)
        {
            return false;
        }

        await authState.ReplaceTokensAsync(refreshed.AccessToken, refreshed.RefreshToken, refreshed.AccessTokenExpiresAt);
        return true;
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value ?? throw new AdminApiException(
                "The server returned an empty response.", (int)response.StatusCode, "empty_response");
        }

        throw await BuildExceptionAsync(response, cancellationToken);
    }

    private static async Task<AdminApiException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ProblemDetailsDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Not every failure response is a ProblemDetails body (a reverse proxy's own
            // error page, for instance) - fall back to a status-based message below.
        }

        var message = problem?.Detail ?? FallbackMessage((int)response.StatusCode);
        return new AdminApiException(message, (int)response.StatusCode, problem?.ErrorCode, problem?.Errors);
    }

    private static string FallbackMessage(int status) => status switch
    {
        401 => "Your session has expired. Please sign in again.",
        403 => "You do not have access to do that.",
        404 => "That could not be found.",
        >= 500 => "The server had a problem. Please try again.",
        _ => "The request could not be completed.",
    };
}
