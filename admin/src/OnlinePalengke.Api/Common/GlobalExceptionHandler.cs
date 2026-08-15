using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OnlinePalengke.Application.Common;

namespace OnlinePalengke.Api.Common;

/// <summary>
/// Turns exceptions into RFC 7807 ProblemDetails responses.
/// </summary>
/// <remarks>
/// Expected failures (<see cref="AppException"/>) carry their own status code, a stable
/// machine-readable error code, and a message written for the caller. Anything else is
/// an unexpected fault: it is logged with its stack trace and returned as a bare 500
/// with no detail, because an arbitrary exception message may contain connection
/// strings, SQL, or another user's data.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validation => BuildValidationProblem(validation),
            TooManyRequestsException tooMany => BuildTooManyRequestsProblem(httpContext, tooMany),
            AppException app => BuildProblem(app.StatusCode, TitleFor(app.StatusCode), app.Message, app.ErrorCode),
            OperationCanceledException => null, // The caller went away; there is nobody to answer.
            _ => BuildUnexpectedProblem(),
        };

        if (problemDetails is null)
        {
            return false;
        }

        if (exception is AppException expected)
        {
            logger.LogInformation(
                "{ErrorCode} on {Method} {Path}: {Message}",
                expected.ErrorCode, httpContext.Request.Method, httpContext.Request.Path, expected.Message);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static ProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var problem = BuildProblem(
            exception.StatusCode, "Validation failed", exception.Message, exception.ErrorCode);

        if (exception.Errors.Count > 0)
        {
            problem.Extensions["errors"] = exception.Errors;
        }

        return problem;
    }

    private static ProblemDetails BuildTooManyRequestsProblem(HttpContext httpContext, TooManyRequestsException exception)
    {
        if (exception.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        return BuildProblem(exception.StatusCode, "Too many requests", exception.Message, exception.ErrorCode);
    }

    private static ProblemDetails BuildUnexpectedProblem() =>
        BuildProblem(
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred",
            "Something went wrong on our side. Please try again.",
            "internal_error");

    private static ProblemDetails BuildProblem(int status, string title, string detail, string errorCode) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Extensions = { ["errorCode"] = errorCode },
        };

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Request failed",
    };
}
