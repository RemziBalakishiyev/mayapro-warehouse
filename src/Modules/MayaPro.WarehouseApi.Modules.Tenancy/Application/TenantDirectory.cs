using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application;

/// <summary>
/// The Tenancy module's implementation of <see cref="ITenantDirectory"/> — the only way other modules learn
/// whether a tenant exists and may sign in. Read-only and untracked; one primary-key lookup per call.
/// <para>
/// BE#36: the same single lookup now also carries <c>ExpiresAt</c>, so the per-request gate can enforce the
/// subscription without a second query.
/// </para>
/// </summary>
internal sealed class TenantDirectory(TenancyDbContext db) : ITenantDirectory
{
    public async Task<TenantInfo?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return null;

        Tenant? tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return tenant is null
            ? null
            : new TenantInfo(tenant.Id, tenant.Name, tenant.Status.ToString(), tenant.IsActive, tenant.ExpiresAt);
    }
}
