using System.Data.Common;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure;

/// <summary>
/// The DayEnd module's DbContext. Owns the <c>dayend</c> schema. Participates in cross-module transactions
/// via <see cref="ITransactionalDbContext"/> so a closing and its activity log commit together.
/// </summary>
public sealed class DayEndDbContext(DbContextOptions<DayEndDbContext> options)
    : DbContext(options), IDayEndDbContext, ITransactionalDbContext
{
    public const string Schema = "dayend";

    public DbSet<Closing> Closings => Set<Closing>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DayEndDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
