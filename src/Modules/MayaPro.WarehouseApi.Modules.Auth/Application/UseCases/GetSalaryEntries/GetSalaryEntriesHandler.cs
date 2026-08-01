using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalaryEntries;

/// <summary>
/// One employee's salary account lines for a single accounting month, newest first. The filter is the
/// entry's <c>Month</c> (which month it settles), not its <c>Date</c> (when the cash moved) — see
/// <see cref="SalaryEntry"/>. An omitted <c>month</c> means the current business month; a malformed one is
/// rejected (400) rather than silently ignored. A month with no lines is an empty list, not a 404.
/// </summary>
public sealed class GetSalaryEntriesHandler(IAuthDbContext db, IDateProvider dateProvider)
{
    public async Task<Result<IReadOnlyList<SalaryEntryDto>>> Handle(Guid userId, string? month, CancellationToken ct)
    {
        string filter;
        if (string.IsNullOrWhiteSpace(month))
            filter = SalaryMonth.From(dateProvider.Today);
        else if (!SalaryMonth.TryParse(month, out filter))
            return Result.Failure<IReadOnlyList<SalaryEntryDto>>(SalaryErrors.InvalidMonth);

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct))
            return Result.Failure<IReadOnlyList<SalaryEntryDto>>(AuthErrors.UserNotFound);

        List<SalaryEntry> entries = await db.SalaryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Month == filter)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<SalaryEntryDto>>(entries.Select(e => e.ToDto()).ToList());
    }
}
