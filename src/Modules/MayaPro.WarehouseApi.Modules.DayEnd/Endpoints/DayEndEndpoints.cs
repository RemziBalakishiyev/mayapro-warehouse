using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetClosings;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetTodayClosing;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Endpoints;

internal static class DayEndEndpoints
{
    // Matches the host's role policy: only Owner may close the day.
    private const string OwnerOnly = "OwnerOnly";

    public static void MapDayEndEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/closings")
            .WithTags("DayEnd")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (GetClosingsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetClosings");

        group.MapGet("/today", async (GetTodayClosingHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetTodayClosing");

        group.MapPost("/", async (
                CloseDayCommand command,
                CloseDayHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/closings/{result.Value.Id}" : "/api/closings";
                return result.ToCreatedResult(location);
            })
            .RequireAuthorization(OwnerOnly)
            .WithName("CloseDay");
    }
}
