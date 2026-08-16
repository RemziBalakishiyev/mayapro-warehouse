using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetPlatformStats;

/// <summary>
/// <c>GET /api/admin/stats</c> — how many shops are in each state and how much came in this month.
/// <para>
/// "This month" is the calendar month of the <b>business</b> time zone (Asia/Baku), converted to the UTC
/// window the rows are stored in — the same rule every other "today"/"this month" figure in the system uses
/// (<see cref="IDateProvider"/>), so the admin's number agrees with the shop-side reports.
/// </para>
/// </summary>
public sealed class GetPlatformStatsHandler(TenancyDbContext db, IDateProvider dateProvider)
{
    public async Task<PlatformStatsDto> Handle(CancellationToken ct)
    {
        DateTime now = dateProvider.UtcNow;

        int active = await db.Tenants.CountAsync(t => t.Status == TenantStatus.Active, ct);
        int pending = await db.Tenants.CountAsync(t => t.Status == TenantStatus.PendingApproval, ct);
        int blocked = await db.Tenants.CountAsync(t => t.Status == TenantStatus.Blocked, ct);

        // Locked out by the subscription rather than by a support decision: Active, but the period lapsed.
        // Open-ended shops (ExpiresAt == null) are never counted here — they cannot expire.
        int expired = await db.Tenants.CountAsync(
            t => t.Status == TenantStatus.Active && t.ExpiresAt != null && t.ExpiresAt <= now, ct);

        DateOnly today = dateProvider.Today;
        DateOnly firstOfMonth = new(today.Year, today.Month, 1);
        (DateTime startUtc, _) = dateProvider.LocalDayRangeUtc(firstOfMonth);
        (DateTime nextMonthStartUtc, _) = dateProvider.LocalDayRangeUtc(firstOfMonth.AddMonths(1));

        decimal collected = await db.SubscriptionPayments
            .Where(p => p.PaidAt >= startUtc && p.PaidAt < nextMonthStartUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new PlatformStatsDto(active, pending, blocked, expired, collected);
    }
}
