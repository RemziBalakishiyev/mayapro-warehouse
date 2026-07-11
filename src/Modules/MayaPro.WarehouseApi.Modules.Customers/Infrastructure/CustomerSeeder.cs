using MayaPro.WarehouseApi.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure;

/// <summary>
/// Development seeder: if the Customers table is empty, inserts the four demo customers from the frontend
/// <c>seed.ts</c>. Debt is the outstanding balance (seed totalDebt − paidAmount).
/// </summary>
public sealed class CustomerSeeder(CustomersDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Customers.AnyAsync(ct))
            return;

        Customer[] customers =
        [
            Customer.Create("Rəşad Məmmədov (Bina bazar)", "994501112233", null, debt: 440),
            Customer.Create("Aygün Əliyeva", "994552223344", null, debt: 0),
            Customer.Create("Elvin Quliyev (8-ci km)", "994703334455", null, debt: 1250),
            Customer.Create("Nigar Həsənova", "994514445566", null, debt: 460)
        ];

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);
    }
}
