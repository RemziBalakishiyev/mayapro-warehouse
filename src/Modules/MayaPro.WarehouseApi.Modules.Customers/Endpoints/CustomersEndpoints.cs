using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomerPayments;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetCustomers;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Customers.Endpoints;

internal static class CustomersEndpoints
{
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
    }

    /// <summary>Body for <c>POST /api/customers/{id}/payments</c> — the id comes from the route.</summary>
    private sealed record AddCustomerPaymentRequest(decimal Amount, string? Note);
}
