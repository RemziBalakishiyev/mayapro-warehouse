using System.Text.Json;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Api.Middleware;

/// <summary>
/// BE#35/BE#36 — the per-request tenant gate. It runs after authentication and enforces the rules the
/// isolation and subscription guarantees depend on:
/// <list type="number">
///   <item><b>A platform admin is not a shop.</b> A <c>PlatformAdmin</c> token belongs to no
///   <c>tenancy.Tenants</c> row, so both checks below are meaningless for it and it passes straight through —
///   neither 401 nor 403. This is the <i>only</i> exemption, and it is keyed on the role claim alone, so an
///   ordinary token still has to satisfy everything. What stops the admin from reading shop data is not this
///   middleware but the query filters: its reserved tenant id owns no business row anywhere, so every
///   tenant-scoped query returns empty (fail-closed).</item>
///   <item><b>An authenticated request must name a tenant.</b> A token minted before multi-tenancy (or
///   forged without the claim) would otherwise reach the endpoints with an empty tenant context. The query
///   filters would return nothing rather than everything, but "silently empty" is a bad contract — and a
///   token that cannot be scoped has no business being honoured. → <c>401</c>.</item>
///   <item><b>The tenant must still be allowed in.</b> Login already refuses a blocked or unapproved shop,
///   but tokens outlive that decision, so the check is repeated per request. → <c>403</c>, same message as
///   login.</item>
///   <item><b>The subscription must still be paid (BE#36).</b> An <c>Active</c> shop whose <c>ExpiresAt</c>
///   has passed is refused with <c>Auth.SubscriptionExpiredForbidden</c>. The shop's <b>status is never
///   changed</b> — the date is the verdict — so recording a payment re-opens it on the very next request
///   with no background job and no state to repair. A shop with no <c>ExpiresAt</c> is open-ended and can
///   never fall into this branch. The check costs nothing extra: <see cref="TenantInfo"/> already carries
///   the expiry, so this is still <b>one</b> lookup per request.</item>
/// </list>
/// Anonymous requests (login, registration, the public invoice link, health, Swagger) pass straight through
/// — they carry no identity to gate.
/// <para>
/// Cost: one primary-key lookup on <c>tenancy.Tenants</c> per authenticated request, on the connection the
/// scope already owns. It is deliberately not cached so that blocking a shop (or its subscription lapsing)
/// takes effect on the very next request; caching is noted as a future optimisation in
/// <c>docs/multi-tenancy.md</c>.
/// </para>
/// </summary>
public sealed class TenantGateMiddleware(RequestDelegate next)
{
    private const string TenantMissingCode = "Auth.TenantMissing";
    private const string TenantMissingMessage = "Token mağaza məlumatı daşımır — yenidən daxil olun";

    private const string TenantInactiveCode = "Auth.TenantInactiveForbidden";
    private const string TenantInactiveMessage = "Mağaza aktiv deyil";

    private const string PendingApprovalCode = "Auth.TenantPendingApprovalForbidden";
    private const string PendingApprovalMessage = "Hesabınız təsdiq gözləyir";

    private const string BlockedCode = "Auth.TenantBlockedForbidden";
    private const string SubscriptionExpiredCode = "Auth.SubscriptionExpiredForbidden";

    private const string PendingApprovalStatus = "PendingApproval";

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant currentTenant,
        ITenantDirectory tenants,
        IDateProvider dateProvider,
        IOptions<PlatformAdminOptions> platformAdmin)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // Rule 1 — the platform operator. Checked before everything else: it has no tenant to look up.
        if (context.User.IsInRole(PlatformAdminAccess.RoleName))
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

        if (tenant is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, TenantInactiveCode, TenantInactiveMessage);
            return;
        }

        string supportMessage = SubscriptionMessage(platformAdmin.Value.Phone);

        if (!tenant.IsActive)
        {
            bool pending = string.Equals(tenant.Status, PendingApprovalStatus, StringComparison.Ordinal);
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                pending ? PendingApprovalCode : BlockedCode,
                pending ? PendingApprovalMessage : supportMessage);
            return;
        }

        if (tenant.IsSubscriptionExpired(dateProvider.UtcNow))
        {
            await WriteErrorAsync(
                context, StatusCodes.Status403Forbidden, SubscriptionExpiredCode, supportMessage);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Mirrors <c>AuthErrors.SubscriptionMessage</c>. Duplicated rather than referenced because the host
    /// does not depend on the Auth module's types — the two are pinned together by an integration test that
    /// compares the login body with the per-request body.
    /// </summary>
    private static string SubscriptionMessage(string? supportPhone) =>
        string.IsNullOrWhiteSpace(supportPhone)
            ? "Abunəliyiniz bitib — dəstək ilə əlaqə saxlayın"
            : $"Abunəliyiniz bitib — əlaqə: {supportPhone}";

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
