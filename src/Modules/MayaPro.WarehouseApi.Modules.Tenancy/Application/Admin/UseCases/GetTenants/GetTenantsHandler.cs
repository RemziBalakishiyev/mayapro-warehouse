using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenants;

/// <summary>
/// The platform admin's shop list: status filter plus a free-text search over name / owner / phone, with
/// the billing summary each row shows (last payment, total paid, whether the period has lapsed).
/// <para>
/// <b>No filter bypass here.</b> Both tables this reads — <c>Tenants</c> and <c>SubscriptionPayments</c> —
/// are platform-level and carry no global query filter at all, so the admin surface needs no
/// <c>IgnoreQueryFilters()</c>. That is the whole point of keeping <c>SubscriptionPayment</c> outside
/// <c>ITenantScoped</c>: the module that is allowed to see every shop is also the module with nothing to
/// bypass.
/// </para>
/// </summary>
public sealed class GetTenantsHandler(TenancyDbContext db, IDateProvider dateProvider)
{
    public async Task<Result<IReadOnlyList<TenantListItemDto>>> Handle(GetTenantsQuery query, CancellationToken ct)
    {
        TenantStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out TenantStatus parsed))
                return Result.Failure<IReadOnlyList<TenantListItemDto>>(
                    Error.Validation("Naməlum mağaza statusu"));

            status = parsed;
        }

        IQueryable<Tenant> tenants = db.Tenants.AsNoTracking();

        if (status is { } wanted)
            tenants = tenants.Where(t => t.Status == wanted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Lowered on both sides, exactly like the expense-type duplicate check: the same operation on
            // either side behaves identically on SQL Server and on the in-memory provider.
            string term = query.Search.Trim().ToLower();
            tenants = tenants.Where(t =>
                t.Name.ToLower().Contains(term) ||
                (t.OwnerName != null && t.OwnerName.ToLower().Contains(term)) ||
                (t.Phone != null && t.Phone.ToLower().Contains(term)));
        }

        List<Tenant> rows = await tenants.OrderBy(t => t.Name).ToListAsync(ct);

        List<Guid> ids = rows.Select(t => t.Id).ToList();

        // Platform scale: tens of shops, hundreds of payment rows. Materialising them and folding in memory
        // keeps "last payment amount" (which SQL cannot express as a plain aggregate) readable and correct.
        List<SubscriptionPayment> payments = await db.SubscriptionPayments
            .AsNoTracking()
            .Where(p => ids.Contains(p.TenantId))
            .ToListAsync(ct);

        Dictionary<Guid, List<SubscriptionPayment>> byTenant = payments
            .GroupBy(p => p.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PaidAt).ToList());

        DateTime now = dateProvider.UtcNow;

        List<TenantListItemDto> items = rows
            .Select(t =>
            {
                byTenant.TryGetValue(t.Id, out List<SubscriptionPayment>? paid);
                SubscriptionPayment? last = paid?.FirstOrDefault();

                return new TenantListItemDto(
                    t.Id,
                    t.Name,
                    t.OwnerName,
                    t.Phone,
                    t.Status.ToString(),
                    t.ExpiresAt,
                    t.MonthlyFee,
                    t.IsSubscriptionExpired(now),
                    last?.PaidAt,
                    last?.Amount,
                    paid?.Sum(p => p.Amount) ?? 0m);
            })
            .ToList();

        return Result.Success<IReadOnlyList<TenantListItemDto>>(items);
    }
}
