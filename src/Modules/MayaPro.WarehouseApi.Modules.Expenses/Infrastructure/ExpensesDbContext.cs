using System.Data.Common;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;

/// <summary>
/// The Expenses module's DbContext. Owns the <c>expenses</c> schema. Participates in cross-module
/// transactions via <see cref="ITransactionalDbContext"/> — this is what lets an expense share the
/// product-cost transaction.
/// <para>
/// BE#35: every entity here is tenant-scoped, so <c>OnModelCreating</c> installs the shared global query
/// filter. <paramref name="currentTenant"/> is optional purely so unit tests can new the context up with
/// options alone; in the host it is always injected.
/// </para>
/// </summary>
public sealed class ExpensesDbContext(
    DbContextOptions<ExpensesDbContext> options,
    ICurrentTenant? currentTenant = null)
    : DbContext(options), IExpensesDbContext, ITransactionalDbContext, ITenantAwareDbContext
{
    public const string Schema = "expenses";

    public Guid CurrentTenantId => currentTenant?.TenantId ?? Guid.Empty;

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();

    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Database.UseTransactionAsync(transaction, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpensesDbContext).Assembly);
        modelBuilder.ApplyTenantIsolation(this);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
