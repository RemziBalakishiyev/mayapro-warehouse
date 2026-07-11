using MayaPro.WarehouseApi.Modules.Activity.Application.UseCases.GetActivity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Activity.Endpoints;

internal static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/activity")
            .WithTags("Activity")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (int? take, int? skip, GetActivityHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(take ?? 50, skip ?? 0, ct)))
            .WithName("GetActivity");
    }
}
