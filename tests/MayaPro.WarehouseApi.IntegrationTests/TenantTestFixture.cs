using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#35 — provisioning helper for the tenant-isolation tests. Mərhələ 1 exposes no tenant-registration
/// endpoint on purpose (that is Mərhələ 2), so a second shop is created the only way it can be: by writing
/// the <c>tenancy.Tenants</c> row and its Owner user straight to the test database, exactly as the
/// Mərhələ 2 registration flow eventually will.
/// <para>
/// The contexts are constructed by hand, which means they carry no <c>ICurrentTenant</c> and therefore no
/// <c>TenantInterceptor</c>: the tenant is named explicitly via <see cref="TenantEntity.AssignTenant"/>,
/// the same way the startup seeder does. Everything the tests then assert goes through the real HTTP API.
/// </para>
/// </summary>
internal static class TenantTestFixture
{
    /// <summary>The shop every pre-BE#35 row (and the whole demo seed) belongs to.</summary>
    public static Guid DefaultTenantId => TenantDefaults.DefaultTenantId;

    /// <summary>Creates a shop plus one Owner user, and returns what a test needs to sign in as them.</summary>
    public static async Task<TenantHandle> CreateTenantAsync(
        string storeName,
        string ownerPhone,
        string ownerName = "Test Sahibkar",
        string password = IntegrationTestHelpers.DemoPassword,
        TenantStatus status = TenantStatus.Active)
    {
        Guid tenantId = Guid.NewGuid();

        await using (TenancyDbContext tenancy = NewContext<TenancyDbContext>(o => new TenancyDbContext(o)))
        {
            tenancy.Tenants.Add(Tenant.CreateWithId(tenantId, storeName, ownerName, ownerPhone, status));
            await tenancy.SaveChangesAsync();
        }

        Guid userId = await AddUserAsync(tenantId, ownerName, ownerPhone, password, UserRole.Owner);

        return new TenantHandle(tenantId, storeName, ownerPhone, password, userId);
    }

    /// <summary>Adds one more user to an existing shop — used by the duplicate-phone login tests.</summary>
    public static async Task<Guid> AddUserAsync(
        Guid tenantId,
        string fullName,
        string phone,
        string password,
        UserRole role)
    {
        await using AuthDbContext auth = NewContext<AuthDbContext>(o => new AuthDbContext(o));

        User user = User.Create(fullName, phone, null, new BCryptPasswordHasher().Hash(password), role);
        user.AssignTenant(tenantId);
        auth.Users.Add(user);
        await auth.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Flips a shop's status — the blocked/pending access tests drive AC-9 through this.</summary>
    public static async Task SetStatusAsync(Guid tenantId, TenantStatus status)
    {
        await using TenancyDbContext tenancy = NewContext<TenancyDbContext>(o => new TenancyDbContext(o));

        Tenant tenant = await tenancy.Tenants.SingleAsync(t => t.Id == tenantId);
        switch (status)
        {
            case TenantStatus.Active:
                tenant.Activate();
                break;
            case TenantStatus.Blocked:
                tenant.Block();
                break;
            default:
                tenant.MarkPendingApproval();
                break;
        }

        await tenancy.SaveChangesAsync();
    }

    /// <summary>
    /// Reads a tenant-scoped table across <b>all</b> shops. Only assertions use this — it is how a test
    /// proves the other tenant's row is still there (or still unchanged) after a cross-tenant attempt.
    /// </summary>
    public static async Task<TResult> QueryAcrossTenantsAsync<TContext, TResult>(
        Func<DbContextOptions<TContext>, TContext> factory,
        Func<TContext, Task<TResult>> query)
        where TContext : DbContext
    {
        await using TContext context = NewContext(factory);
        return await query(context);
    }

    private static TContext NewContext<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        DbContextOptions<TContext> options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(WarehouseApiFactory.TestConnectionString)
            .Options;

        return factory(options);
    }

    /// <summary>A provisioned shop: its id, its display name and the credentials of its Owner.</summary>
    internal sealed record TenantHandle(
        Guid TenantId,
        string StoreName,
        string OwnerPhone,
        string OwnerPassword,
        Guid OwnerUserId);
}
