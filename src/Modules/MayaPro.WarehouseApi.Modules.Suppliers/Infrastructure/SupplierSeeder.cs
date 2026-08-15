using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;

/// <summary>
/// Development seeder: if the Suppliers table is empty, inserts the four demo suppliers from the frontend
/// <c>seed.ts</c>. Debt is what we still owe them (seed totalDebt − paidAmount).
/// <para>
/// BE#35: seeders run at startup, outside any request, so there is no tenant context. The demo data belongs
/// to the default shop and says so explicitly; the emptiness check ignores the query filter, which would
/// otherwise report "empty" on every boot and re-seed forever.
/// </para>
/// </summary>
public sealed class SupplierSeeder(SuppliersDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Suppliers.IgnoreQueryFilters().AnyAsync(ct))
            return;

        Supplier[] suppliers =
        [
            Supplier.Create("İstanbul Tekstil (Laleli)", null, "+994502223344", null, debt: 3000),
            Supplier.Create("Guangzhou Ayaqqabı MMC", null, "+994515556677", null, debt: 3200),
            Supplier.Create("Bakı Toptan Aksesuar", null, "+994703334455", null, debt: 0),
            Supplier.Create("Merter Cins Toptan", null, "+994554447788", null, debt: 4000)
        ];

        foreach (Supplier supplier in suppliers)
            supplier.AssignTenant(TenantDefaults.DefaultTenantId);

        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync(ct);
    }
}
