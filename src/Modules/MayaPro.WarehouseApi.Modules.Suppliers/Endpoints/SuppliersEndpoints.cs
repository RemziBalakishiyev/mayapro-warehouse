using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.DeleteSupplier;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierPayments;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSuppliers;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.UpdateSupplier;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Endpoints;

internal static class SuppliersEndpoints
{
    // Host role policies: writing supplier data is Owner/Manager; deleting a supplier is Owner-only.
    private const string OwnerOrManager = "OwnerOrManager";
    private const string OwnerOnly = "OwnerOnly";

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

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateSupplierCommand command,
                UpdateSupplierHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(command with { Id = id }, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("UpdateSupplier");

        // A supplier we still owe cannot be deleted (→ 409); their payment history is removed with them.
        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteSupplierHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOnly)
            .WithName("DeleteSupplier");
    }

    /// <summary>Body for the supplier debt/payment endpoints — the id comes from the route.</summary>
    private sealed record SupplierAmountRequest(decimal Amount, string? Note);
}
