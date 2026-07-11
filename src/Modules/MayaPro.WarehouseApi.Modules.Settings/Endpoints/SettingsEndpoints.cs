using MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.GetSettings;
using MayaPro.WarehouseApi.Modules.Settings.Application.UseCases.UpdateSettings;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Settings.Endpoints;

internal static class SettingsEndpoints
{
    // Matches the host's role policy: only Owner may change settings.
    private const string OwnerOnly = "OwnerOnly";

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization(); // viewing is open to every authenticated role

        group.MapGet("/", async (GetSettingsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("GetSettings");

        group.MapPut("/", async (
                UpdateSettingsCommand command,
                UpdateSettingsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(OwnerOnly)
            .WithName("UpdateSettings");
    }
}
