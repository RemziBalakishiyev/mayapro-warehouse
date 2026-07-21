using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.DeleteSale;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSaleById;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSales;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Sales.Endpoints;

internal static class SalesEndpoints
{
    // Matches the host's role policy: editing/deleting a sale is limited to Owner or Manager.
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization(); // selling is open to every role — sellers are the main users

        group.MapGet("/", async (
                string? date,
                string? from,
                string? to,
                int? take,
                int? skip,
                GetSalesHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.Handle(date, from, to, take, skip, ct)))
            .WithName("GetSales");

        // Full detail of one sale, including customer name (credit) and the product's current name.
        group.MapGet("/{id:guid}", async (
                Guid id,
                GetSaleByIdHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .WithName("GetSaleById");

        group.MapPost("/", async (
                CreateSaleCommand command,
                CreateSaleHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/sales/{result.Value.Id}" : "/api/sales";
                return result.ToCreatedResult(location);
            })
            .WithName("CreateSale");

        // Reverse-and-reapply edit: old effects unwound, new values applied on the same row (date preserved).
        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateSaleCommand command,
                UpdateSaleHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(command with { Id = id }, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("UpdateSale");

        // Deletes the sale and unwinds its chain (stock returns, credit debt reduces).
        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteSaleHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("DeleteSale");
    }
}
