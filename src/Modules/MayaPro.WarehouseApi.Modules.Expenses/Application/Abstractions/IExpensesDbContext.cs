using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;

/// <summary>The Expenses module's data surface. Handlers depend on this, not on the concrete DbContext.</summary>
public interface IExpensesDbContext
{
    DbSet<Expense> Expenses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
