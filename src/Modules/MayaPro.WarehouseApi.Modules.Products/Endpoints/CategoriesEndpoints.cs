using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateCategory;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetCategories;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Products.Endpoints;

internal static class CategoriesEndpoints
{
    public static void MapCategoriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Both reading and creating are open to every authenticated role — by product decision a seller may
        // add a category too (unlike product create, which stays Owner/Manager only).
        RouteGroupBuilder group = endpoints.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization();

        group.MapGet("/", async (GetCategoriesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetCategories");

        group.MapPost("/", async (
                CreateCategoryCommand command,
                CreateCategoryHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/categories/{result.Value.Id}" : "/api/categories";
                return result.ToCreatedResult(location);
            })
            .WithName("CreateCategory");
    }
}
