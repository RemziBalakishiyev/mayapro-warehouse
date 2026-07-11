using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Infrastructure;

/// <summary>
/// The Customers module's DbContext. Owns the <c>customers</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/>.
/// </summary>
public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options)
    : DbContext(options), ICustomersDbContext, ITransactionalDbContext
{
    public const string Schema = "customers";

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
