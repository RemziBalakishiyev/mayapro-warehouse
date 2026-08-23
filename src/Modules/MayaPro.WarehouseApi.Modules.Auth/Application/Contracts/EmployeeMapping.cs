using MayaPro.WarehouseApi.Modules.Auth.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>Maps the <see cref="Employee"/> entity to its wire DTO (BE#57).</summary>
public static class EmployeeMapping
{
    public static EmployeeDto ToDto(this Employee employee) =>
        new(
            employee.Id,
            employee.FullName,
            employee.Phone,
            employee.Position,
            employee.MonthlySalary,
            employee.IsActive,
            employee.Note,
            employee.CreatedAt);
}
