using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Expenses.Endpoints;

internal static class ExpensesEndpoints
{
    // Matches the host's role policy: only Owner or Manager may record expenses.
    private const string OwnerOrManager = "OwnerOrManager";

    public static void MapExpensesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/expenses")
            .WithTags("Expenses")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (string? month, GetExpensesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(month, ct)))
            .WithName("GetExpenses");

        group.MapPost("/", async (
                CreateExpenseCommand command,
                CreateExpenseHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/expenses/{result.Value.Id}" : "/api/expenses";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOrManager)
            .WithName("CreateExpense");
    }
}
