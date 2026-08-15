using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure;

/// <summary>
/// The Customers module's DbContext. Owns the <c>customers</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/>.
/// <para>
/// BE#35: every entity here is tenant-scoped, so <c>OnModelCreating</c> installs the shared global query
/// filter. <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with
/// options alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class CustomersDbContext(
    DbContextOptions<CustomersDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), ICustomersDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "customers";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<CustomerDebtAdjustment> CustomerDebtAdjustments => Set<CustomerDebtAdjustment>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
