using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;

/// <summary>
/// The payroll register, newest first. BE#57: this reads <c>Employees</c>, not login accounts.
/// <para>
/// Deactivated employees are <b>included</b>, carrying <c>isActive: false</c>. Hiding them would make an
/// earlier month's salary summary — whose row order is this very list — quietly lose people who worked it.
/// </para>
/// </summary>
public sealed class GetEmployeesHandler(IAuthDbContext db)
{
    public async Task<IReadOnlyList<EmployeeDto>> Handle(CancellationToken ct)
    {
        List<Employee> employees = await db.Employees
            .AsNoTracking()
            // The Id tiebreaker is not cosmetic: rows seeded (or migrated) in one SaveChanges share a
            // CreatedAt to the tick, and GET /salary-summary has to hand back the very same order.
            .OrderByDescending(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(ct);

        return employees.Select(e => e.ToDto()).ToList();
    }
}
