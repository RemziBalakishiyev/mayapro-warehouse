using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.ApproveTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.CreateTenant;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetPlatformStats;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenantPayments;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenants;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.RecordPayment;
using MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.SetTenantStatus;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Endpoints;

/// <summary>
/// BE#36 — the platform operator's surface. Every route in the group requires
/// <see cref="PlatformAdminAccess.Policy"/>, so an ordinary Sahibkar token gets a <c>403</c> here no matter
/// how it reached the route; the policy is declared once on the group, never per endpoint, so a new endpoint
/// cannot be added unguarded.
/// <para>
/// Everything below reads and writes only the <c>tenancy</c> schema, whose two tables carry no tenant query
/// filter — which is why the admin surface contains no <c>IgnoreQueryFilters()</c> call at all. See the
/// architecture test that pins that down.
/// </para>
/// </summary>
internal static class AdminTenantsEndpoints
{
    public static void MapAdminTenantsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin")
            .WithTags("PlatformAdmin")
            .RequireAuthorization(PlatformAdminAccess.Policy);

        group.MapGet("/tenants", async (
                string? status,
                string? search,
                GetTenantsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new GetTenantsQuery(status, search), ct);
                return result.ToHttpResult();
            })
            .WithName("AdminGetTenants");

        group.MapPost("/tenants", async (
                CreateTenantCommand command,
                CreateTenantHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(command, ct);
                string location = result.IsSuccess ? $"/api/admin/tenants/{result.Value.Id}" : "/api/admin/tenants";
                return result.ToCreatedResult(location);
            })
            .WithName("AdminCreateTenant");

        group.MapPost("/tenants/{id:guid}/approve", async (
                Guid id,
                ApproveTenantCommand command,
                ApproveTenantHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(id, command, ct);
                return result.ToHttpResult();
            })
            .WithName("AdminApproveTenant");

        group.MapPost("/tenants/{id:guid}/block", async (
                Guid id,
                SetTenantStatusHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.BlockAsync(id, ct);
                return result.ToHttpResult();
            })
            .WithName("AdminBlockTenant");

        group.MapPost("/tenants/{id:guid}/unblock", async (
                Guid id,
                SetTenantStatusHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.UnblockAsync(id, ct);
                return result.ToHttpResult();
            })
            .WithName("AdminUnblockTenant");

        group.MapPost("/tenants/{id:guid}/payments", async (
                Guid id,
                RecordPaymentCommand command,
                RecordPaymentHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(id, command, ct);
                return result.ToHttpResult();
            })
            .WithName("AdminRecordSubscriptionPayment");

        group.MapGet("/tenants/{id:guid}/payments", async (
                Guid id,
                GetTenantPaymentsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(id, ct);
                return result.ToHttpResult();
            })
            .WithName("AdminGetSubscriptionPayments");

        group.MapGet("/stats", async (GetPlatformStatsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(ct)))
            .WithName("AdminGetPlatformStats");
    }
}
