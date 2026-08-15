namespace OnlinePalengke.Application.Common;

/// <summary>
/// Base for failures the application expects and can describe to a caller.
/// </summary>
/// <remarks>
/// The API's exception handler maps these to RFC 7807 ProblemDetails with the right
/// status code. Anything that is not an <see cref="AppException"/> is treated as an
/// unexpected fault: logged with a stack trace and returned as a bare 500 with no
/// internal detail, because that path may be carrying information a caller must not see.
/// </remarks>
public abstract class AppException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The HTTP status this failure maps to.</summary>
    public abstract int StatusCode { get; }

    /// <summary>A stable, machine-readable code so clients can branch without string-matching.</summary>
    public abstract string ErrorCode { get; }
}

/// <summary>The request was malformed or violated a business rule on its inputs. 400.</summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string message)
        : base(message) => Errors = new Dictionary<string, string[]>();

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;

    public ValidationException(string field, string error)
        : base("One or more validation errors occurred.") =>
        Errors = new Dictionary<string, string[]> { [field] = [error] };

    /// <summary>Field-level detail, keyed by field name. May be empty for a general message.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override int StatusCode => StatusCodes400;

    public override string ErrorCode => "validation_failed";

    private const int StatusCodes400 = 400;
}

/// <summary>
/// The caller presented no credential, or the one presented (a refresh token,
/// an OTP-derived session) is invalid, expired, or revoked. 401.
/// </summary>
/// <remarks>
/// Distinct from the 401 that ASP.NET Core's authentication middleware
/// already returns automatically when an <c>[Authorize]</c>-gated endpoint
/// gets no valid bearer token — that path never reaches application code at
/// all. This exception exists for the cases application logic decides for
/// itself inside an otherwise-anonymous endpoint, chiefly refresh-token
/// validation: the endpoint accepts any request body, but the token inside
/// it can still turn out to be unusable.
/// </remarks>
public sealed class UnauthorizedAppException(string message) : AppException(message)
{
    public override int StatusCode => 401;

    public override string ErrorCode => "unauthorized";
}

/// <summary>The caller is authenticated but not permitted to do this. 403.</summary>
/// <remarks>
/// Deliberately distinct from a 404. Returning 403 confirms the resource exists, which
/// is the right trade-off for this application's resources but would not be for, say,
/// another customer's order — use <see cref="NotFoundException"/> there instead.
/// </remarks>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;

    public override string ErrorCode => "forbidden";
}

/// <summary>The addressed resource does not exist, or the caller may not know that it does. 404.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;

    public override string ErrorCode => "not_found";
}

/// <summary>The caller is doing this too often — OTP requests chief among them. 429.</summary>
public sealed class TooManyRequestsException(string message, TimeSpan? retryAfter = null) : AppException(message)
{
    /// <summary>How long the caller should wait before trying again, if known.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public override int StatusCode => 429;

    public override string ErrorCode => "rate_limited";
}

/// <summary>
/// The request is valid but conflicts with current state — a closed quote window, an
/// already-awarded order, a duplicate submission. 409.
/// </summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;

    public override string ErrorCode => "conflict";
}
