using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;
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
                Results.Ok(await handler.Handle(Today(), ct)))
            .WithName("GetDashboard");

        group.MapGet("/summary", async (string? period, GetSummaryHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(period, Today(), ct)))
            .WithName("GetSummary");
    }

    // "Today" is resolved on the server so the report window never depends on the client's clock.
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
