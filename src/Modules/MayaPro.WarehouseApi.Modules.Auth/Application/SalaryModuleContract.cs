using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application;

/// <summary>
/// The Auth module's implementation of <see cref="ISalaryModule"/>: day total and report rows, mirroring
/// <c>ExpensesModuleContract</c>. Day boundaries are the business time zone's (via
/// <see cref="IDateProvider"/>), so a payment made at 00:30 Baku counts against the Baku day.
/// <para>
/// Both methods filter to <see cref="SalaryEntryType.Payment"/> only: a deduction never leaves the drawer,
/// so it must not reach day-end's expected cash or the dashboard's expense figure.
/// </para>
/// </summary>
internal sealed class SalaryModuleContract(IAuthDbContext db, IDateProvider dateProvider) : ISalaryModule
{
    public async Task<decimal> GetDayPaymentsTotalAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        (DateTime start, DateTime end) = dateProvider.LocalDayRangeUtc(date);

        return await db.SalaryEntries
            .AsNoTracking()
            .Where(e => e.Type == SalaryEntryType.Payment && e.Date >= start && e.Date < end)
            .SumAsync(e => e.Amount, cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryPaymentRow>> GetPaymentsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SalaryEntry> query = db.SalaryEntries
            .AsNoTracking()
            .Where(e => e.Type == SalaryEntryType.Payment);

        if (from is { } f)
            query = query.Where(e => e.Date >= dateProvider.LocalDayRangeUtc(f).StartUtc);
        if (to is { } t)
            query = query.Where(e => e.Date < dateProvider.LocalDayRangeUtc(t).EndUtc);

        var rows = await query
            .OrderBy(e => e.Date)
            .Select(e => new { e.Date, e.UserId, e.Amount })
            .ToListAsync(cancellationToken);

        // Names are resolved in a separate lookup rather than an inner join, so a payment can never drop out
        // of the report (and out of the dashboard's cash maths) because of a missing user row.
        Dictionary<Guid, string> names = await db.Users
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return rows
            .Select(r => new SalaryPaymentRow(
                dateProvider.ToLocalDate(r.Date),
                r.UserId,
                names.TryGetValue(r.UserId, out string? name) ? name : string.Empty,
                r.Amount))
            .ToList();
    }
}
