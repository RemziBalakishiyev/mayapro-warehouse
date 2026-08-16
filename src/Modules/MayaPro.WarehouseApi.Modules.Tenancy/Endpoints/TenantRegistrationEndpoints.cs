using MayaPro.WarehouseApi.Modules.Tenancy.Application.UseCases.RegisterTenant;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Endpoints;

/// <summary>
/// BE#36 — self-service shop registration. It is mapped under <c>/api/auth</c> because that is the surface a
/// signed-out visitor already knows, but it lives in the Tenancy module: what it creates is a
/// <b>shop</b>, and the owner user is a consequence, requested from the Auth module through
/// <c>IIdentityProvisioning</c>. Auth keeps owning login; Tenancy keeps owning shops.
/// <para>
/// <b>Rate limiting:</b> this endpoint is anonymous and writes to the database, so it wants a per-IP limit
/// like the public invoice link has. That is deliberately left to Mərhələ 3 — noted here and in
/// <c>docs/multi-tenancy.md</c> so it is not forgotten.
/// </para>
/// </summary>
internal static class TenantRegistrationEndpoints
{
    public static void MapTenantRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
                RegisterTenantCommand command,
                RegisterTenantHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                // No Location header target exists: a shop has no shop-facing endpoint of its own, and the
                // caller cannot sign in yet. 201 with the body is the honest answer.
                return result.IsSuccess
                    ? Results.Created(string.Empty, result.Value)
                    : result.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("RegisterTenant");
    }
}
