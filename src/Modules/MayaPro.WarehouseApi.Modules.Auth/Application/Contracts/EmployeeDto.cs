namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>
/// An employee row for <c>GET /api/employees</c>. <c>MonthlySalary</c> was added additively by BE#28 and is
/// <c>0</c> (never null) for an employee whose salary has not been set.
/// </summary>
public sealed record EmployeeDto(
    Guid Id,
    string FullName,
    string Phone,
    string Role,
    bool IsActive,
    decimal MonthlySalary);
