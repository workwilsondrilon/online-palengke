using Microsoft.AspNetCore.Mvc;
using OnlinePalengke.Application.Uploads;

namespace OnlinePalengke.Api.Endpoints;

/// <summary>
/// The presign/commit upload pair, mapped identically under every role group.
/// </summary>
/// <remarks>
/// The same two endpoints appear at <c>/api/customer/uploads</c>,
/// <c>/api/partner/uploads</c> and so on. The route prefix is not what authorizes the
/// call — <see cref="Domain.Media.MediaPurposePolicy"/> checks the caller's actual role
/// against the requested purpose, so a partner hitting the customer route gains nothing.
/// Mapping per group exists so each app has a consistent base path and so the OpenAPI
/// document is grouped the way each client team reads it.
/// </remarks>
public static class UploadEndpoints
{
    /// <param name="group">The role group to map into.</param>
    /// <param name="routeNamePrefix">
    /// Distinguishes the generated endpoint names across groups — ASP.NET Core requires
    /// endpoint names to be unique across the whole application, and these endpoints are
    /// mapped four times.
    /// </param>
    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder group, string routeNamePrefix)
    {
        var uploads = group.MapGroup("/uploads").WithTags("Uploads");

        uploads.MapPost("/presign", PresignAsync)
            .WithName($"{routeNamePrefix}.PresignUpload")
            .WithSummary("Requests permission to upload one file directly to object storage.")
            .Produces<PresignUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        uploads.MapPost("/{assetId:long}/commit", CommitAsync)
            .WithName($"{routeNamePrefix}.CommitUpload")
            .WithSummary("Confirms an upload landed in object storage.")
            .Produces<CommitUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> PresignAsync(
        [FromBody] PresignUploadRequest request,
        UploadService uploads,
        CancellationToken cancellationToken)
    {
        var response = await uploads.PresignAsync(request, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> CommitAsync(
        long assetId,
        UploadService uploads,
        CancellationToken cancellationToken)
    {
        var response = await uploads.CommitAsync(assetId, cancellationToken);

        return Results.Ok(response);
    }
}
