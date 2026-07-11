using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// Development seeder: if the Users table is empty, inserts four demo employees
/// (1 Owner, 1 Manager, 2 Sellers). All share the password "demo123".
/// </summary>
public sealed class UserSeeder(AuthDbContext db, IPasswordHasher passwordHasher)
{
    private const string DemoPassword = "demo123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
            return;

        string hash = passwordHasher.Hash(DemoPassword);

        User[] users =
        [
            User.Create("Rəşad Məmmədov", "0501112233", "resad@sederek.az", hash, UserRole.Owner),
            User.Create("Nigar Əliyeva", "0552223344", "nigar@sederek.az", hash, UserRole.Manager),
            User.Create("Elvin Hüseynov", "0553334455", "elvin@sederek.az", hash, UserRole.Seller),
            User.Create("Günel Quliyeva", "0554445566", "gunel@sederek.az", hash, UserRole.Seller)
        ];

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
    }
}
