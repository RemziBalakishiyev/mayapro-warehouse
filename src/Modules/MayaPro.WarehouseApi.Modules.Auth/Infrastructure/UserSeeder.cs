using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// Development seeder: if the Users table is empty, inserts four demo employees
/// (1 Owner, 1 Manager, 2 Sellers). All share the password "demo123".
/// <para>
/// BE#35: the seeder runs at startup, outside any request, so there is no tenant context for
/// <c>TenantInterceptor</c> to use. It therefore names the tenant itself — the default shop the data
/// migrations back-fill everything else to — and reads the table with
/// <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/> so the emptiness check sees
/// every shop's users, not zero rows through an empty tenant filter (which would re-seed on every boot).
/// </para>
/// </summary>
public sealed class UserSeeder(AuthDbContext db, IPasswordHasher passwordHasher)
{
    private const string DemoPassword = "demo123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
            return;

        string hash = passwordHasher.Hash(DemoPassword);

        User[] users =
        [
            User.Create("Rəşad Məmmədov", "0501112233", "resad@sederek.az", hash, UserRole.Owner),
            User.Create("Nigar Əliyeva", "0552223344", "nigar@sederek.az", hash, UserRole.Manager),
            User.Create("Elvin Hüseynov", "0553334455", "elvin@sederek.az", hash, UserRole.Seller),
            User.Create("Günel Quliyeva", "0554445566", "gunel@sederek.az", hash, UserRole.Seller)
        ];

        foreach (User user in users)
            user.AssignTenant(TenantDefaults.DefaultTenantId);

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
    }
}
