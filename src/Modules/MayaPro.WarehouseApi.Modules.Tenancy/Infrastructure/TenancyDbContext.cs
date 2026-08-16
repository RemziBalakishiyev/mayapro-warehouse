using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;

/// <summary>
/// The Tenancy module's DbContext. Owns the <c>tenancy</c> schema — the registry of shops the rest of the
/// system is partitioned by, plus (BE#36) the subscription payments the platform records against them.
/// <para>
/// It is the one context with <b>no</b> tenant query filter, for the obvious reason: it defines the
/// tenants. Neither of its two tables is <c>ITenantScoped</c>, which is also why the module needs no
/// <c>IgnoreQueryFilters()</c> anywhere — there is nothing to ignore.
/// </para>
/// <para>
/// BE#36 enlists it in the cross-module transaction (<see cref="ITransactionalDbContext"/>): registration
/// writes a <c>tenancy.Tenants</c> row and an <c>identity.Users</c> row, and a shop with no owner (or an
/// owner with no shop) must never be a reachable state.
/// </para>
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : DbContext(options), ITransactionalDbContext
{
    public const string Schema = "tenancy";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
    }

    /// <summary>Money is <c>decimal(18,2)</c> everywhere in this codebase — the fee and the payments too.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
