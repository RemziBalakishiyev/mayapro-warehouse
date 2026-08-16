using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenantPayments;

/// <summary>
/// One shop's payment history, newest first. An unknown shop is a <c>404</c> rather than an empty list, so
/// the admin UI can tell "no payments yet" from "wrong id".
/// </summary>
public sealed class GetTenantPaymentsHandler(TenancyDbContext db)
{
    public async Task<Result<IReadOnlyList<SubscriptionPaymentDto>>> Handle(Guid tenantId, CancellationToken ct)
    {
        bool exists = await db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!exists)
            return Result.Failure<IReadOnlyList<SubscriptionPaymentDto>>(TenancyErrors.TenantNotFound);

        List<SubscriptionPaymentDto> payments = await db.SubscriptionPayments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new SubscriptionPaymentDto(
                p.Id, p.TenantId, p.Amount, p.PaidAt, p.PeriodMonths, p.Note, p.RecordedByAdminId))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<SubscriptionPaymentDto>>(payments);
    }
}
