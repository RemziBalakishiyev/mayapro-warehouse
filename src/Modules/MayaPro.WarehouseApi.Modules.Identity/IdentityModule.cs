using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MayaPro.WarehouseApi.Modules.Identity;

/// <summary>
/// Identity module skeleton. For now it only proves the module mechanism end to end:
/// no services yet, and a single temporary <c>GET /api/identity/ping</c> endpoint.
/// Real users / login / JWT arrive in the next stage.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty — no Identity services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/identity").WithTags("Identity");

        group.MapGet("/ping", () => Results.Ok("pong"))
            .WithName("IdentityPing");
    }

    public Task MigrateAsync(IServiceProvider services) => Task.CompletedTask;
}
