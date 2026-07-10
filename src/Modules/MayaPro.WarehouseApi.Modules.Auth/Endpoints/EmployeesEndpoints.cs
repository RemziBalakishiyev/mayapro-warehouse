using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Auth.Endpoints;

internal static class EmployeesEndpoints
{
    public static void MapEmployeesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/employees")
            .WithTags("Employees")
            .RequireAuthorization(); // open to every role for now

        group.MapGet("/", async (GetEmployeesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetEmployees");
    }
}
