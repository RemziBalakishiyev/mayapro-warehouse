using MayaPro.WarehouseApi.Modules.Tenancy.Domain;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;

/// <summary>
/// What a successful <c>POST /api/auth/register</c> returns. Deliberately thin: no token is issued, because
/// the shop is <c>PendingApproval</c> and cannot sign in yet — the message is what the UI shows.
/// </summary>
public sealed record RegistrationResultDto(Guid TenantId, string StoreName, string Status, string Message);

/// <summary>
/// One row of <c>GET /api/admin/tenants</c>. Everything the platform operator needs to judge a shop at a
/// glance, computed server-side so the admin UI does no arithmetic.
/// </summary>
public sealed record TenantListItemDto(
    Guid Id,
    string Name,
    string? OwnerName,
    string? Phone,
    string Status,
    DateTime? ExpiresAt,
    decimal MonthlyFee,
    bool IsExpired,
    DateTime? LastPaymentAt,
    decimal? LastPaymentAmount,
    decimal TotalPaid);

/// <summary>
/// What the four state-changing admin commands (approve / block / unblock / record payment) echo back: the
/// shop's new subscription state, so the UI never has to re-fetch the list to know what happened.
/// </summary>
public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string Status,
    DateTime? ExpiresAt,
    decimal MonthlyFee,
    bool IsExpired)
{
    public static TenantSummaryDto From(Tenant tenant, DateTime utcNow) => new(
        tenant.Id,
        tenant.Name,
        tenant.Status.ToString(),
        tenant.ExpiresAt,
        tenant.MonthlyFee,
        tenant.IsSubscriptionExpired(utcNow));
}

/// <summary>One row of <c>GET /api/admin/tenants/{id}/payments</c>.</summary>
public sealed record SubscriptionPaymentDto(
    Guid Id,
    Guid TenantId,
    decimal Amount,
    DateTime PaidAt,
    int PeriodMonths,
    string? Note,
    Guid? RecordedByAdminId);

/// <summary>
/// <c>GET /api/admin/stats</c>. <see cref="ExpiredCount"/> counts shops that are still <c>Active</c> but
/// whose paid period has lapsed — they are locked out by <c>ExpiresAt</c>, not by their status, which is
/// exactly the distinction the operator needs to see.
/// <para>
/// BE#42 — <see cref="CollectedThisMonth"/> is the specified name for the current Baku calendar month's
/// takings (ADR-0005).
/// </para>
/// </summary>
public sealed record PlatformStatsDto(
    int ActiveCount,
    int PendingCount,
    int BlockedCount,
    int ExpiredCount,
    decimal CollectedThisMonth);
