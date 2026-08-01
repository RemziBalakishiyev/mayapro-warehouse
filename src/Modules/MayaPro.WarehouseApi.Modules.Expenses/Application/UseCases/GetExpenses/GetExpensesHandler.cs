using System.Globalization;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;

/// <summary>
/// Returns expenses, newest first. Date filters:
/// <list type="bullet">
///   <item><c>from</c>/<c>to</c> (ISO <c>yyyy-MM-dd</c>) — inclusive date range; when either is present,
///     <c>month</c> is ignored entirely.</item>
///   <item><c>month</c> (<c>yyyy-MM</c>) — only that month's expenses, used when no from/to is given.</item>
///   <item>none of the above — all expenses (existing behaviour).</item>
/// </list>
/// An optional <c>source</c> ("general" | "product") narrows further — an unrecognised value is rejected
/// (400) rather than silently ignored.
/// </summary>
public sealed class GetExpensesHandler(IExpensesDbContext db)
{
    public async Task<Result<IReadOnlyList<ExpenseDto>>> Handle(
        string? month, string? source, CancellationToken ct, string? from = null, string? to = null)
    {
        ExpenseSource? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (!ExpenseSourceCode.TryParse(source, out ExpenseSource parsed))
                return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidSource);
            sourceFilter = parsed;
        }

        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        bool hasRange = !string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to);

        if (hasRange)
        {
            // from/to take precedence over month — a plain inclusive day range, no timezone conversion
            // (expense dates are stored/filtered as plain local dates, matching the month filter below).
            if (!string.IsNullOrWhiteSpace(from)
                && DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fromDate))
            {
                query = query.Where(e => e.Date >= fromDate.Date);
            }

            if (!string.IsNullOrWhiteSpace(to)
                && DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime toDate))
            {
                DateTime toExclusive = toDate.Date.AddDays(1);
                query = query.Where(e => e.Date < toExclusive);
            }
        }
        else if (!string.IsNullOrWhiteSpace(month)
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
