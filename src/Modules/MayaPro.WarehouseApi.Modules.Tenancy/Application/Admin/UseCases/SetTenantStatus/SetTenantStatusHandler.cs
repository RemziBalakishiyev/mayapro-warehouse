using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.SetTenantStatus;

/// <summary>
/// Block / unblock — the two manual status switches behind <c>POST /api/admin/tenants/{id}/block</c> and
/// <c>.../unblock</c>. One handler, because they are the same write with a different target state.
/// <para>
/// Neither touches <c>ExpiresAt</c>: blocking is a support decision ("abuse", "we are done with this
/// customer") and is independent of whether the subscription is paid. Unblocking therefore restores exactly
/// the subscription the shop had — if that period has already lapsed the shop is still locked out, this time
/// by the expiry rule, and recording a payment is what opens it.
/// </para>
/// </summary>
public sealed class SetTenantStatusHandler(TenancyDbContext db, IDateProvider dateProvider)
{
    public Task<Result<TenantSummaryDto>> BlockAsync(Guid tenantId, CancellationToken ct) =>
        ApplyAsync(tenantId, t => t.Block(), ct);

    public Task<Result<TenantSummaryDto>> UnblockAsync(Guid tenantId, CancellationToken ct) =>
        ApplyAsync(tenantId, t => t.Activate(), ct);

    private async Task<Result<TenantSummaryDto>> ApplyAsync(
        Guid tenantId,
        Action<Tenant> change,
        CancellationToken ct)
    {
        Tenant? tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return Result.Failure<TenantSummaryDto>(TenancyErrors.TenantNotFound);

        change(tenant);
        await db.SaveChangesAsync(ct);

        return Result.Success(TenantSummaryDto.From(tenant, dateProvider.UtcNow));
    }
}
