using System.Globalization;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;

/// <summary>
/// Returns expenses, newest first. With a <c>month</c> (<c>yyyy-MM</c>) only that month's expenses are
/// returned; without it, all expenses. An optional <c>source</c> ("general" | "product") narrows further —
/// an unrecognised value is rejected (400) rather than silently ignored.
/// </summary>
public sealed class GetExpensesHandler(IExpensesDbContext db)
{
    public async Task<Result<IReadOnlyList<ExpenseDto>>> Handle(string? month, string? source, CancellationToken ct)
    {
        ExpenseSource? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (!ExpenseSourceCode.TryParse(source, out ExpenseSource parsed))
                return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidSource);
            sourceFilter = parsed;
        }

        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedMonth))
        {
            var start = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
            DateTime end = start.AddMonths(1);
            query = query.Where(e => e.Date >= start && e.Date < end);
        }

        if (sourceFilter is { } filter)
            query = query.Where(e => e.Source == filter);

        List<Expense> expenses = await query
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ExpenseDto>>(expenses.Select(e => e.ToDto()).ToList());
    }
}
