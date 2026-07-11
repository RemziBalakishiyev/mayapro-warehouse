using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application;

/// <summary>
/// The Expenses module's implementation of <see cref="IExpensesModule"/>: day total and report rows.
/// Day boundaries are the business time zone's (via <see cref="IDateProvider"/>).
/// </summary>
internal sealed class ExpensesModuleContract(IExpensesDbContext db, IDateProvider dateProvider) : IExpensesModule
{
    public async Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        (DateTime start, DateTime end) = dateProvider.LocalDayRangeUtc(date);

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
            query = query.Where(e => e.Date >= dateProvider.LocalDayRangeUtc(f).StartUtc);
        if (to is { } t)
            query = query.Where(e => e.Date < dateProvider.LocalDayRangeUtc(t).EndUtc);

        List<Expense> expenses = await query.OrderBy(e => e.Date).ToListAsync(cancellationToken);

        return expenses
            .Select(e => new ExpenseReportRow(dateProvider.ToLocalDate(e.Date), e.Category.ToCode(), e.Amount))
            .ToList();
    }
}
