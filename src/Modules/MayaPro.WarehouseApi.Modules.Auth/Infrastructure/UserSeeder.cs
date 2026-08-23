using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// Development seeder for the <c>identity</c> schema. It fills two now-separate registers (BE#57):
/// <list type="number">
/// <item>four demo <b>login accounts</b> (1 Owner, 1 Manager, 2 Sellers), all with the password "demo123";</item>
/// <item>three demo <b>payroll records</b> — the same three people the owner would actually be paying. They
/// mirror the non-Owner demo users by name and contact number, which is exactly what the real BE#57 data
/// migration produces from an existing database.</item>
/// </list>
/// The two steps are independently idempotent on purpose: an installation that already has users but no
/// employees (the state a developer's database is in the moment this ships) still gets its payroll seeded.
/// <para>
/// BE#35: the seeder runs at startup, outside any request, so there is no tenant context for
/// <c>TenantInterceptor</c> to use. It therefore names the tenant itself — the default shop the data
/// migrations back-fill everything else to — and reads each table with
/// <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/> so the emptiness check sees
/// every shop's rows, not zero rows through an empty tenant filter (which would re-seed on every boot).
/// </para>
/// </summary>
public sealed class UserSeeder(AuthDbContext db, IPasswordHasher passwordHasher)
{
    private const string DemoPassword = "demo123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedUsersAsync(ct);
        await SeedEmployeesAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
            return;

        string hash = passwordHasher.Hash(DemoPassword);

        User[] users =
        [
            User.Create("Rəşad Məmmədov", Canonical("0501112233"), "resad@sederek.az", hash, UserRole.Owner),
            User.Create("Nigar Əliyeva", Canonical("0552223344"), "nigar@sederek.az", hash, UserRole.Manager),
            User.Create("Elvin Hüseynov", Canonical("0553334455"), "elvin@sederek.az", hash, UserRole.Seller),
            User.Create("Günel Quliyeva", Canonical("0554445566"), "gunel@sederek.az", hash, UserRole.Seller)
        ];

        foreach (User user in users)
            user.AssignTenant(TenantDefaults.DefaultTenantId);

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// BE#57 — the payroll register. <c>Position</c> is free text, so the demo rows read the way a shop owner
    /// would actually write them ("Menecer", "Satıcı"), not as role codes.
    /// </summary>
    private async Task SeedEmployeesAsync(CancellationToken ct)
    {
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(ct))
            return;

        Employee[] employees =
        [
            Employee.Create("Nigar Əliyeva", Canonical("0552223344"), "Menecer"),
            Employee.Create("Elvin Hüseynov", Canonical("0553334455"), "Satıcı"),
            Employee.Create("Günel Quliyeva", Canonical("0554445566"), "Satıcı")
        ];

        foreach (Employee employee in employees)
            employee.AssignTenant(TenantDefaults.DefaultTenantId);

        db.Employees.AddRange(employees);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// BE#46 — the demo phones are written in the local form a person would type, and stored in the canonical
    /// one the column now holds (<c>0501112233</c> → <c>994501112233</c>). Routing them through the shared
    /// normalizer rather than hard-coding the canonical string keeps the seed and the rule from ever drifting
    /// apart; signing in still works with whichever spelling the developer types.
    /// </summary>
    private static string Canonical(string phone) => PhoneNormalizer.Normalize(phone).Value;
}
