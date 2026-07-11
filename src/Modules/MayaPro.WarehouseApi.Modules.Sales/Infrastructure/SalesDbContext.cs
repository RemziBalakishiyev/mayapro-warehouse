using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Infrastructure;

/// <summary>
/// The Sales module's DbContext. Owns the <c>sales</c> schema. Participates in cross-module transactions
/// via <see cref="ITransactionalDbContext"/> — this is what lets a sale share the stock/debt transaction.
/// </summary>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options)
    : DbContext(options), ISalesDbContext, ITransactionalDbContext
{
    public const string Schema = "sales";

    public DbSet<Sale> Sales => Set<Sale>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
