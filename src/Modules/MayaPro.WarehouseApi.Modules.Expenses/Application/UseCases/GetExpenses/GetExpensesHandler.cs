using System.Globalization;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenses;

/// <summary>
/// Returns expenses, newest first. With a <c>month</c> (<c>yyyy-MM</c>) only that month's expenses are
/// returned; without it, all expenses. <c>from</c>/<c>to</c> (<c>yyyy-MM-dd</c>, inclusive on both ends)
/// filter by an explicit date range and, when either is present, take over completely — <c>month</c> is
/// then ignored. An optional <c>source</c> ("general" | "product") narrows further — an unrecognised
/// <c>source</c>, a malformed <c>from</c>/<c>to</c>, or <c>from</c> later than <c>to</c> is rejected (400)
/// rather than silently ignored.
/// </summary>
public sealed class GetExpensesHandler(IExpensesDbContext db)
{
    private const string DateFormat = "yyyy-MM-dd";

    public async Task<Result<IReadOnlyList<ExpenseDto>>> Handle(
        string? month, string? source, string? from, string? to, CancellationToken ct)
    {
        ExpenseSource? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (!ExpenseSourceCode.TryParse(source, out ExpenseSource parsed))
                return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidSource);
            sourceFilter = parsed;
        }

        DateTime? fromDate = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParseExact(from, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedFrom))
                return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidDateRange);
            fromDate = parsedFrom.Date;
        }

        DateTime? toDate = null;
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParseExact(to, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTo))
                return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidDateRange);
            toDate = parsedTo.Date;
        }

        if (fromDate is { } f && toDate is { } t && f > t)
            return Result.Failure<IReadOnlyList<ExpenseDto>>(ExpenseErrors.InvalidDateRange);

        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (fromDate is not null || toDate is not null)
        {
            // from/to take over completely — month is ignored (AC-4) even if both were supplied.
            if (fromDate is { } start)
                query = query.Where(e => e.Date >= start);
            if (toDate is { } end)
            {
                DateTime exclusiveEnd = end.AddDays(1); // "to" is inclusive of the whole day.
                query = query.Where(e => e.Date < exclusiveEnd);
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
