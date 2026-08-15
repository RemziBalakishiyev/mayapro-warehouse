using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;

/// <summary>
/// The Tenancy module's DbContext. Owns the <c>tenancy</c> schema — the registry of shops the rest of the
/// system is partitioned by.
/// <para>
/// It is the one context with <b>no</b> tenant query filter, for the obvious reason: it defines the
/// tenants. It also stays out of the cross-module transaction (no <c>ITransactionalDbContext</c>) because
/// nothing in a business chain writes to it.
/// </para>
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    public const string Schema = "tenancy";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
    }
}
