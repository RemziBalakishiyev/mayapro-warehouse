using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;

/// <summary>Returns all users as employee rows, newest first.</summary>
public sealed class GetEmployeesHandler(IAuthDbContext db)
{
    public async Task<IReadOnlyList<EmployeeDto>> Handle(CancellationToken ct)
    {
        // Materialise first — Role.ToCode() is a C# mapping EF cannot translate to SQL.
        var users = await db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.FullName, u.Phone, u.Role, u.IsActive })
            .ToListAsync(ct);

        return users
            .Select(u => new EmployeeDto(u.Id, u.FullName, u.Phone, u.Role.ToCode(), u.IsActive))
            .ToList();
    }
}
