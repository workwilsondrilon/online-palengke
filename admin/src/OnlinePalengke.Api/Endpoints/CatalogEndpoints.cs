using Microsoft.AspNetCore.Mvc;
using OnlinePalengke.Application.Catalog;

namespace OnlinePalengke.Api.Endpoints;

/// <summary>Admin CRUD for categories, units and items, mapped under <c>/api/admin</c>.</summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this RouteGroupBuilder adminGroup)
    {
        MapCategoryEndpoints(adminGroup);
        MapUnitEndpoints(adminGroup);
        MapItemEndpoints(adminGroup);
    }

    private static void MapCategoryEndpoints(RouteGroupBuilder adminGroup)
    {
        var categories = adminGroup.MapGroup("/categories").WithTags("Admin.Categories");

        categories.MapGet("/", async (CategoryService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)))
            .WithName("Admin.Categories.List")
            .Produces<IReadOnlyList<CategoryResponse>>();

        categories.MapGet("/{id:long}", async (long id, CategoryService service, CancellationToken ct) =>
                Results.Ok(await service.GetAsync(id, ct)))
            .WithName("Admin.Categories.Get")
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/", async ([FromBody] CreateCategoryRequest request, CategoryService service, CancellationToken ct) =>
                Results.Ok(await service.CreateAsync(request, ct)))
            .WithName("Admin.Categories.Create")
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapPut("/{id:long}", async (long id, [FromBody] UpdateCategoryRequest request, CategoryService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .WithName("Admin.Categories.Update")
            .Produces<CategoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapDelete("/{id:long}", async (long id, CategoryService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("Admin.Categories.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapUnitEndpoints(RouteGroupBuilder adminGroup)
    {
        var units = adminGroup.MapGroup("/units").WithTags("Admin.Units");

        units.MapGet("/", async (UnitService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)))
            .WithName("Admin.Units.List")
            .Produces<IReadOnlyList<UnitResponse>>();

        units.MapGet("/{id:long}", async (long id, UnitService service, CancellationToken ct) =>
                Results.Ok(await service.GetAsync(id, ct)))
            .WithName("Admin.Units.Get")
            .Produces<UnitResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        units.MapPost("/", async ([FromBody] CreateUnitRequest request, UnitService service, CancellationToken ct) =>
                Results.Ok(await service.CreateAsync(request, ct)))
            .WithName("Admin.Units.Create")
            .Produces<UnitResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        units.MapPut("/{id:long}", async (long id, [FromBody] UpdateUnitRequest request, UnitService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .WithName("Admin.Units.Update")
            .Produces<UnitResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        units.MapDelete("/{id:long}", async (long id, UnitService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("Admin.Units.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapItemEndpoints(RouteGroupBuilder adminGroup)
    {
        var items = adminGroup.MapGroup("/items").WithTags("Admin.Items");

        items.MapGet("/", async ([FromQuery] long? categoryId, ItemService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(categoryId, ct)))
            .WithName("Admin.Items.List")
            .Produces<IReadOnlyList<ItemResponse>>();

        items.MapGet("/{id:long}", async (long id, ItemService service, CancellationToken ct) =>
                Results.Ok(await service.GetAsync(id, ct)))
            .WithName("Admin.Items.Get")
            .Produces<ItemResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        items.MapPost("/", async ([FromBody] CreateItemRequest request, ItemService service, CancellationToken ct) =>
                Results.Ok(await service.CreateAsync(request, ct)))
            .WithName("Admin.Items.Create")
            .Produces<ItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        items.MapPut("/{id:long}", async (long id, [FromBody] UpdateItemRequest request, ItemService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .WithName("Admin.Items.Update")
            .Produces<ItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items.MapDelete("/{id:long}", async (long id, ItemService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .WithName("Admin.Items.Delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
