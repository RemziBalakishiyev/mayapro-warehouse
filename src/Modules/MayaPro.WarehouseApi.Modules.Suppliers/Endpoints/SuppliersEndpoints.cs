using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierPayments;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSuppliers;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Endpoints;

internal static class SuppliersEndpoints
{
    // Matches the host's role policy: only Sahibkar or Menecer may write supplier data.
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapSuppliersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (GetSuppliersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetSuppliers");

        group.MapPost("/", async (
                CreateSupplierCommand command,
                CreateSupplierHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/suppliers/{result.Value.Id}" : "/api/suppliers";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("CreateSupplier");

        group.MapPost("/{id:guid}/debts", async (
                Guid id,
                SupplierAmountRequest request,
                AddSupplierDebtHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new AddSupplierDebtCommand(id, request.Amount, request.Note), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("AddSupplierDebt");

        group.MapGet("/{id:guid}/payments", async (
                Guid id,
                GetSupplierPaymentsHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.Handle(id, ct)))
            .WithName("GetSupplierPayments");

        group.MapPost("/{id:guid}/payments", async (
                Guid id,
                SupplierAmountRequest request,
                AddSupplierPaymentHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new AddSupplierPaymentCommand(id, request.Amount, request.Note), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("AddSupplierPayment");
    }

    /// <summary>Body for the supplier debt/payment endpoints — the id comes from the route.</summary>
    private sealed record SupplierAmountRequest(decimal Amount, string? Note);
}
