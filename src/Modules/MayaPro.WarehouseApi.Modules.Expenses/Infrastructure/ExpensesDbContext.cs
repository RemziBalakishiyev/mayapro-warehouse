using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

/// <summary>
/// The Expenses module's DbContext. Owns the <c>expenses</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/> — this is what lets an expense share the
/// product-cost transaction.
/// </summary>
public sealed class ExpensesDbContext(DbContextOptions<ExpensesDbContext> options)
    : DbContext(options), IExpensesDbContext, ITransactionalDbContext
{
    public const string Schema = "expenses";

    public DbSet<Expense> Expenses => Set<Expense>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpensesDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
