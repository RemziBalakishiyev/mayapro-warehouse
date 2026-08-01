using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDashboard;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDebtsKpi;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetProductsKpi;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSalesKpi;
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

        // BE#27 — page KPI endpoints: from/to (both optional, ISO yyyy-MM-dd) bound the period-scoped
        // fields; an absent from/to means the whole history.
        group.MapGet("/products-kpi", async (
                DateOnly? from, DateOnly? to, GetProductsKpiHandler handler, CancellationToken ct) =>
                (await handler.Handle(from, to, ct)).ToHttpResult())
            .WithName("GetProductsKpi");

        group.MapGet("/sales-kpi", async (
                DateOnly? from, DateOnly? to, GetSalesKpiHandler handler, CancellationToken ct) =>
                (await handler.Handle(from, to, ct)).ToHttpResult())
            .WithName("GetSalesKpi");

        group.MapGet("/debts-kpi", async (
                DateOnly? from, DateOnly? to, GetDebtsKpiHandler handler, CancellationToken ct) =>
                (await handler.Handle(from, to, ct)).ToHttpResult())
            .WithName("GetDebtsKpi");
    }
}
