using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application;

/// <summary>The Expenses module's implementation of <see cref="IExpensesModule"/>: day total for day-end.</summary>
internal sealed class ExpensesModuleContract(IExpensesDbContext db) : IExpensesModule
{
    public async Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        DateTime start = date.ToDateTime(TimeOnly.MinValue);
        DateTime end = start.AddDays(1);

        return await db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= start && e.Date < end)
            .SumAsync(e => e.Amount, cancellationToken);
    }
}
