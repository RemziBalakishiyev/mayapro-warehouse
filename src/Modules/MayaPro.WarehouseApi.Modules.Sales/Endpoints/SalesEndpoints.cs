using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSales;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Sales.Endpoints;

internal static class SalesEndpoints
{
    public static void MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization(); // selling is open to every role — sellers are the main users

        group.MapGet("/", async (string? date, GetSalesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(date, ct)))
            .WithName("GetSales");

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
    }
}
