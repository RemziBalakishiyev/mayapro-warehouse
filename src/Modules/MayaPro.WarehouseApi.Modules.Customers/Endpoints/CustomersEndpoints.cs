using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.DeleteCustomer;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerHistory;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerPayments;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.UpdateCustomer;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Customers.Endpoints;

internal static class CustomersEndpoints
{
    // Host role policies: editing a customer is Owner/Manager; deleting one is Owner-only.
    private const string OwnerOrManager = "OwnerOrManager";
    private const string OwnerOnly = "OwnerOnly";

    public static void MapCustomersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization(); // every authenticated role

        group.MapGet("/", async (GetCustomersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetCustomers");

        group.MapPost("/", async (
                CreateCustomerCommand command,
                CreateCustomerHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/customers/{result.Value.Id}" : "/api/customers";
                return result.ToCreatedResult(location);
            })
            .WithName("CreateCustomer");

        group.MapGet("/{id:guid}/payments", async (
                Guid id,
                GetCustomerPaymentsHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.Handle(id, ct)))
            .WithName("GetCustomerPayments");

        // Full chronological debt history: opening balance, credit sales and payments in one feed.
        group.MapGet("/{id:guid}/history", async (
                Guid id,
                GetCustomerHistoryHandler handler,
                CancellationToken ct) =>
                Results.Ok(await handler.Handle(id, ct)))
            .WithName("GetCustomerHistory");

        group.MapPost("/{id:guid}/payments", async (
                Guid id,
                AddCustomerPaymentRequest request,
                AddCustomerPaymentHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new AddCustomerPaymentCommand(id, request.Amount, request.Note), ct);
                return result.ToHttpResult();
            })
            .WithName("AddCustomerPayment");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCustomerCommand command,
                UpdateCustomerHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(command with { Id = id }, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOrManager)
            .WithName("UpdateCustomer");

        // A customer with outstanding debt cannot be deleted (→ 409); their history is removed with them.
        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteCustomerHandler handler,
                CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttpResult())
            .RequireAuthorization(OwnerOnly)
            .WithName("DeleteCustomer");
    }

    /// <summary>Body for <c>POST /api/customers/{id}/payments</c> — the id comes from the route.</summary>
    private sealed record AddCustomerPaymentRequest(decimal Amount, string? Note);
}
