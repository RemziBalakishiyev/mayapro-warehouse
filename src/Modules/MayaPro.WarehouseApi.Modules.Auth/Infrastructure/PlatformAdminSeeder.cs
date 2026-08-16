using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// BE#36 — creates the single <see cref="UserRole.PlatformAdmin"/> account from the <c>PlatformAdmin</c>
/// configuration section. Runs in <b>every</b> environment (unlike the demo <c>UserSeeder</c>): without it a
/// fresh production install would have no way into <c>/api/admin/*</c> at all.
/// <list type="bullet">
///   <item><b>Idempotent.</b> If a platform admin already exists, nothing happens — no second account, and
///   an existing admin's password is never rewritten from configuration.</item>
///   <item><b>Configuration-driven.</b> Phone and password come from <see cref="PlatformAdminOptions"/>
///   (override with <c>PlatformAdmin__Password</c> in production). Nothing is seeded when they are unset, so
///   an installation cannot inherit a guessable default.</item>
///   <item><b>Tenant.</b> The row carries <see cref="TenantDefaults.PlatformTenantId"/>, a reserved id no
///   shop uses — see that constant for why this beats both <c>NULL</c> and <c>Guid.Empty</c>.</item>
/// </list>
/// <para>
/// The existence check runs with <c>IgnoreQueryFilters()</c>: startup has no tenant context, so the filtered
/// query would see nothing and re-seed on every boot. Same documented exception as every other seeder.
/// </para>
/// </summary>
public sealed class PlatformAdminSeeder(
    IAuthDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<PlatformAdminOptions> options)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        PlatformAdminOptions settings = options.Value;
        if (!settings.IsConfigured)
            return;

        bool exists = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == UserRole.PlatformAdmin, ct);

        if (exists)
            return;

        User admin = User.Create(
            settings.FullName,
            settings.Phone.Trim(),
            email: null,
            passwordHasher.Hash(settings.Password),
            UserRole.PlatformAdmin);

        admin.AssignTenant(TenantDefaults.PlatformTenantId);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
