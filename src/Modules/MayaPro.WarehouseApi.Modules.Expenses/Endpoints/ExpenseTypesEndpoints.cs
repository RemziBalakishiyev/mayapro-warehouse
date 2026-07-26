using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;
using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenseTypes;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Expenses.Endpoints;

internal static class ExpenseTypesEndpoints
{
    public static void MapExpenseTypesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Both reading and creating are open to every authenticated role — same product decision as
        // managed categories (Products module): any role may add an expense type.
        RouteGroupBuilder group = endpoints.MapGroup("/api/expense-types")
            .WithTags("ExpenseTypes")
            .RequireAuthorization();

        group.MapGet("/", async (GetExpenseTypesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetExpenseTypes");

        group.MapPost("/", async (
                CreateExpenseTypeCommand command,
                CreateExpenseTypeHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/expense-types/{result.Value.Id}" : "/api/expense-types";
                return result.ToCreatedResult(location);
            })
            .WithName("CreateExpenseType");
    }
}
