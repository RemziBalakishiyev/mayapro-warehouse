using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.AdjustStock;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProduct;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.GetProducts;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.UpdateProduct;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Products.Endpoints;

internal static class ProductsEndpoints
{
    // Matches the host's role policy: only Owner or Manager may add/edit stock items.
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (GetProductsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetProducts");

        group.MapGet("/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) =>
            {
                var result = await handler.Handle(id, ct);
                return result.ToHttpResult();
            })
            .WithName("GetProduct");

        group.MapPost("/", async (
                CreateProductCommand command,
                CreateProductHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/products/{result.Value.Id}" : "/api/products";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("CreateProduct");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductCommand command,
                UpdateProductHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command with { Id = id }, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("UpdateProduct");

        // Stock corrections are open to every role — a seller can fix stock too.
        group.MapPost("/{id:guid}/adjust-stock", async (
                Guid id,
                AdjustStockRequest request,
                AdjustStockHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new AdjustStockCommand(id, request.Delta, request.Note), ct);
                return result.ToHttpResult();
            })
            .WithName("AdjustStock");
    }

    /// <summary>Body for <c>POST /api/products/{id}/adjust-stock</c> — the id comes from the route.</summary>
    private sealed record AdjustStockRequest(int Delta, string? Note);
}
