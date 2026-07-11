using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Reports.Endpoints;

internal static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/dashboard", async (GetDashboardHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetDashboard");

        group.MapGet("/summary", async (string? period, GetSummaryHandler handler, CancellationToken ct) =>
            {
                var result = await handler.Handle(period, ct);
                return result.ToHttpResult();
            })
            .WithName("GetSummary");
    }
}
