using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalarySummary;

/// <summary>
/// Every employee's salary standing for one accounting month: agreed salary, what has been paid, what has
/// been deducted, and what is left. The filter is the entry's <c>Month</c> (which month it settles), not its
/// <c>Date</c> — paying July's salary in August shows up in July's summary. Employees with no lines at all
/// are still listed (0 / 0 / full salary), and the remainder is allowed to go negative: that means the
/// employee has already been paid more than the month owes. Row order matches <c>GET /api/employees</c>.
/// </summary>
public sealed class GetSalarySummaryHandler(IAuthDbContext db, IDateProvider dateProvider)
{
    public async Task<Result<IReadOnlyList<EmployeeSalarySummaryDto>>> Handle(string? month, CancellationToken ct)
    {
        string filter;
        if (string.IsNullOrWhiteSpace(month))
            filter = SalaryMonth.From(dateProvider.Today);
        else if (!SalaryMonth.TryParse(month, out filter))
            return Result.Failure<IReadOnlyList<EmployeeSalarySummaryDto>>(SalaryErrors.InvalidMonth);

        // Materialise both sides first — Role.ToCode() is a C# mapping EF cannot translate to SQL, and the
        // per-employee totals are a trivial in-memory fold over one month's rows.
        var users = await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.FullName, u.Role, u.MonthlySalary })
            .ToListAsync(ct);

        var entries = await db.SalaryEntries
            .AsNoTracking()
            .Where(e => e.Month == filter)
            .Select(e => new { e.UserId, e.Type, e.Amount })
            .ToListAsync(ct);

        var byUser = entries
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Paid: g.Where(e => e.Type == SalaryEntryType.Payment).Sum(e => e.Amount),
                    Deducted: g.Where(e => e.Type == SalaryEntryType.Deduction).Sum(e => e.Amount)));

        List<EmployeeSalarySummaryDto> rows = users
            .Select(u =>
            {
                (decimal paid, decimal deducted) = byUser.TryGetValue(u.Id, out var totals) ? totals : (0m, 0m);
                return new EmployeeSalarySummaryDto(
                    u.Id,
                    u.FullName,
                    u.Role.ToCode(),
                    u.MonthlySalary,
                    paid,
                    deducted,
                    u.MonthlySalary - paid - deducted);
            })
            .ToList();

        return Result.Success<IReadOnlyList<EmployeeSalarySummaryDto>>(rows);
    }
}
