using Microsoft.AspNetCore.Mvc;
using OnlinePalengke.Application.Markets;

namespace OnlinePalengke.Api.Endpoints;

/// <summary>
/// Admin CRUD for markets and their delivery windows (mapped under <c>/api/admin</c>), plus
/// the customer-facing delivery eligibility check (mapped under <c>/api/customer</c>).
/// </summary>
public static class MarketEndpoints
{
    public static void MapAdminMarketEndpoints(this RouteGroupBuilder adminGroup)
    {
        var markets = adminGroup.MapGroup("/markets").WithTags("Admin.Markets");

        markets.MapGet("/", async (MarketService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)))
            .WithName("Admin.Markets.List")
            .Produces<IReadOnlyList<MarketResponse>>();

        markets.MapGet("/{id:long}", async (long id, MarketService service, CancellationToken ct) =>
                Results.Ok(await service.GetAsync(id, ct)))
            .WithName("Admin.Markets.Get")
            .Produces<MarketResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        markets.MapPost("/", async ([FromBody] CreateMarketRequest request, MarketService service, CancellationToken ct) =>
                Results.Ok(await service.CreateAsync(request, ct)))
            .WithName("Admin.Markets.Create")
            .Produces<MarketResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        markets.MapPut("/{id:long}", async (long id, [FromBody] UpdateMarketRequest request, MarketService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .WithName("Admin.Markets.Update")
            .Produces<MarketResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        markets.MapDelete("/{id:long}", async (long id, MarketService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("Admin.Markets.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        markets.MapPut("/{id:long}/service-area", async (
                long id, [FromBody] UpdateServiceAreaRequest request, MarketService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateServiceAreaAsync(id, request, ct)))
            .WithName("Admin.Markets.UpdateServiceArea")
            .Produces<MarketResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        markets.MapGet("/{marketId:long}/delivery-windows", async (
                long marketId, DeliveryWindowService service, CancellationToken ct) =>
                Results.Ok(await service.ListForMarketAsync(marketId, ct)))
            .WithName("Admin.Markets.DeliveryWindows.List")
            .Produces<IReadOnlyList<DeliveryWindowResponse>>();

        markets.MapPost("/{marketId:long}/delivery-windows", async (
                long marketId, [FromBody] CreateDeliveryWindowRequest request, DeliveryWindowService service, CancellationToken ct) =>
                Results.Ok(await service.CreateAsync(marketId, request, ct)))
            .WithName("Admin.Markets.DeliveryWindows.Create")
            .Produces<DeliveryWindowResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var deliveryWindows = adminGroup.MapGroup("/delivery-windows").WithTags("Admin.DeliveryWindows");

        deliveryWindows.MapPut("/{id:long}", async (
                long id, [FromBody] UpdateDeliveryWindowRequest request, DeliveryWindowService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .WithName("Admin.DeliveryWindows.Update")
            .Produces<DeliveryWindowResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        deliveryWindows.MapDelete("/{id:long}", async (long id, DeliveryWindowService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("Admin.DeliveryWindows.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Maps the customer-facing "will you deliver to this point" check backing decision 14.
    /// </summary>
    public static void MapCustomerMarketEndpoints(this RouteGroupBuilder customerGroup)
    {
        customerGroup.MapGroup("/markets")
            .WithTags("Customer.Markets")
            .MapPost("/eligibility", async ([FromBody] EligibilityRequest request, MarketEligibilityService service, CancellationToken ct) =>
                    Results.Ok(await service.CheckAsync(request, ct)))
                .WithName("Customer.Markets.CheckEligibility")
                .WithSummary("Given a candidate delivery point, returns which markets (if any) deliver there.")
                .Produces<EligibilityResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
