using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

/// <summary>
/// Development seeder: if the ExpenseTypes table is empty, inserts the seven default expense types the UI
/// offers when recording an expense.
/// <para>
/// BE#35: seeders run at startup, outside any request, so there is no tenant context. The defaults belong
/// to the default shop and say so explicitly; the emptiness check ignores the query filter, which would
/// otherwise report "empty" on every boot and re-seed forever.
/// </para>
/// </summary>
public sealed class ExpenseTypeSeeder(ExpensesDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.ExpenseTypes.IgnoreQueryFilters().AnyAsync(ct))
            return;

        string[] names =
        [
            "Yol pulu",
            "Fəhlə pulu",
            "Yer/Anbar xərci",
            "Paket/Qutu",
            "Gömrük",
            "Mağaza xərci",
            "Digər"
        ];

        foreach (string name in names)
        {
            ExpenseType type = ExpenseType.Create(name);
            type.AssignTenant(TenantDefaults.DefaultTenantId);
            db.ExpenseTypes.Add(type);
        }

        await db.SaveChangesAsync(ct);
    }
}
