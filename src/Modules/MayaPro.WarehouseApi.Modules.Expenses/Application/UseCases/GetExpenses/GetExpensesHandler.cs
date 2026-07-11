using System.Globalization;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;

/// <summary>
/// Returns expenses, newest first. With a <c>month</c> (<c>yyyy-MM</c>) only that month's expenses are
/// returned; without it, all expenses.
/// </summary>
public sealed class GetExpensesHandler(IExpensesDbContext db)
{
    public async Task<IReadOnlyList<ExpenseDto>> Handle(string? month, CancellationToken ct)
    {
        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            var start = new DateTime(parsed.Year, parsed.Month, 1);
            DateTime end = start.AddMonths(1);
            query = query.Where(e => e.Date >= start && e.Date < end);
        }

        List<Expense> expenses = await query
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

        return expenses.Select(e => e.ToDto()).ToList();
    }
}
