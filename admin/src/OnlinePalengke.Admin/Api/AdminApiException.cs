namespace OnlinePalengke.Admin.Api;

/// <summary>
/// A failed <see cref="AdminApiClient"/> call, carrying a message that is always safe to
/// show directly to the admin using the screen.
/// </summary>
public sealed class AdminApiException(
    string message,
    int? statusCode,
    string? errorCode,
    IReadOnlyDictionary<string, string[]>? fieldErrors = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;

    public string? ErrorCode { get; } = errorCode;

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; } = fieldErrors ?? new Dictionary<string, string[]>();

    public bool IsUnauthorized => StatusCode == 401;
}
