using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;

/// <summary>
/// The Suppliers module's DbContext. Owns the <c>suppliers</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/>.
/// </summary>
public sealed class SuppliersDbContext(DbContextOptions<SuppliersDbContext> options)
    : DbContext(options), ISuppliersDbContext, ITransactionalDbContext
{
    public const string Schema = "suppliers";

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SuppliersDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
