using System.Text.Json;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Api.Middleware;

/// <summary>
/// BE#35 — the per-request tenant gate. It runs after authentication and enforces two rules that the
/// isolation guarantee depends on:
/// <list type="number">
///   <item><b>An authenticated request must name a tenant.</b> A token minted before multi-tenancy (or
///   forged without the claim) would otherwise reach the endpoints with an empty tenant context. The query
///   filters would return nothing rather than everything, but "silently empty" is a bad contract — and a
///   token that cannot be scoped has no business being honoured. → <c>401</c>.</item>
///   <item><b>The tenant must still be allowed in.</b> Login already refuses a blocked or unapproved shop,
///   but tokens outlive that decision, so the check is repeated per request. → <c>403</c>, same message as
///   login.</item>
/// </list>
/// Anonymous requests (login, the public invoice link, health, Swagger) pass straight through — they carry
/// no identity to gate.
/// <para>
/// Cost: one primary-key lookup on <c>tenancy.Tenants</c> per authenticated request, on the connection the
/// scope already owns. It is deliberately not cached in Mərhələ 1 so that blocking a shop takes effect on
/// the very next request; caching is noted as a future optimisation in <c>docs/multi-tenancy.md</c>.
/// </para>
/// </summary>
public sealed class TenantGateMiddleware(RequestDelegate next)
{
    private const string TenantMissingCode = "Auth.TenantMissing";
    private const string TenantMissingMessage = "Token mağaza məlumatı daşımır — yenidən daxil olun";

    private const string TenantInactiveCode = "Auth.TenantInactiveForbidden";
    private const string TenantInactiveMessage = "Mağaza aktiv deyil";

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant, ITenantDirectory tenants)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (currentTenant.TenantId is not { } tenantId)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, TenantMissingCode, TenantMissingMessage);
            return;
        }

        TenantInfo? tenant = await tenants.FindAsync(tenantId, context.RequestAborted);
        if (tenant is not { IsActive: true })
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, TenantInactiveCode, TenantInactiveMessage);
            return;
        }

        await next(context);
    }

    /// <summary>Same <c>{ code, message }</c> body every other failure uses, so the frontend needs no special case.</summary>
    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new { code, message },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
