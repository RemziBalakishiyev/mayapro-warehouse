using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application;

/// <summary>The Expenses module's implementation of <see cref="IExpensesModule"/>: day total and report rows.</summary>
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

    public async Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (from is { } f)
            query = query.Where(e => e.Date >= f.ToDateTime(TimeOnly.MinValue));
        if (to is { } t)
            query = query.Where(e => e.Date < t.AddDays(1).ToDateTime(TimeOnly.MinValue));

        List<Expense> expenses = await query.OrderBy(e => e.Date).ToListAsync(cancellationToken);

        return expenses
            .Select(e => new ExpenseReportRow(DateOnly.FromDateTime(e.Date), e.Category.ToCode(), e.Amount))
            .ToList();
    }
}
