using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

/// <summary>
/// Development seeder: if the ExpenseTypes table is empty, inserts the seven default expense types the UI
/// offers when recording an expense.
/// </summary>
public sealed class ExpenseTypeSeeder(ExpensesDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.ExpenseTypes.AnyAsync(ct))
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
            db.ExpenseTypes.Add(ExpenseType.Create(name));

        await db.SaveChangesAsync(ct);
    }
}
