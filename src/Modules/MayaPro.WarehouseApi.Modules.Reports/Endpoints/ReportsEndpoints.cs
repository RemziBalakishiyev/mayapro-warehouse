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
        // BE#31 — from/to are bound as raw strings, not DateOnly?: an unparsable value must reach the
        // handler as a normal { code, message } 400, not blow up minimal-API model binding into a 500.
        group.MapGet("/products-kpi", async (
                string? from, string? to, GetProductsKpiHandler handler, CancellationToken ct) =>
            {
                if (!TryParseOptionalDate(from, out DateOnly? fromDate, out string? fromError))
                    return Results.BadRequest(new { code = "Reports.InvalidFrom", message = fromError });
                if (!TryParseOptionalDate(to, out DateOnly? toDate, out string? toError))
                    return Results.BadRequest(new { code = "Reports.InvalidTo", message = toError });

                return (await handler.Handle(fromDate, toDate, ct)).ToHttpResult();
            })
            .WithName("GetProductsKpi");

        group.MapGet("/sales-kpi", async (
                string? from, string? to, GetSalesKpiHandler handler, CancellationToken ct) =>
            {
                if (!TryParseOptionalDate(from, out DateOnly? fromDate, out string? fromError))
                    return Results.BadRequest(new { code = "Reports.InvalidFrom", message = fromError });
                if (!TryParseOptionalDate(to, out DateOnly? toDate, out string? toError))
                    return Results.BadRequest(new { code = "Reports.InvalidTo", message = toError });

                return (await handler.Handle(fromDate, toDate, ct)).ToHttpResult();
            })
            .WithName("GetSalesKpi");

        group.MapGet("/debts-kpi", async (
                string? from, string? to, GetDebtsKpiHandler handler, CancellationToken ct) =>
            {
                if (!TryParseOptionalDate(from, out DateOnly? fromDate, out string? fromError))
                    return Results.BadRequest(new { code = "Reports.InvalidFrom", message = fromError });
                if (!TryParseOptionalDate(to, out DateOnly? toDate, out string? toError))
                    return Results.BadRequest(new { code = "Reports.InvalidTo", message = toError });

                return (await handler.Handle(fromDate, toDate, ct)).ToHttpResult();
            })
            .WithName("GetDebtsKpi");
    }

    // BE#31 — mirrors ExportsEndpoints.TryParseOptionalDate: an absent/blank value is valid (means
    // "unbounded"), while an unparsable one is reported as a { code, message } 400 instead of letting
    // minimal-API binding throw and surface as a 500 through the GlobalExceptionHandler.
    private static bool TryParseOptionalDate(string? raw, out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (DateOnly.TryParse(raw, out DateOnly parsed))
        {
            date = parsed;
            return true;
        }

        error = "Tarix formatı yanlışdır (gözlənilən: yyyy-MM-dd)";
        return false;
    }
}
