namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>
/// An employee (payroll) row for <c>GET /api/employees</c>.
/// <para>
/// BE#57 is a <b>breaking</b> wire change: <c>role</c> is gone — an employee has no role because an employee
/// has no login — and is replaced by the free-text <c>position</c>. <c>phone</c> is now nullable, and
/// <c>note</c> / <c>createdAt</c> are new. See <c>docs/changes/CHANGELOG.md</c>.
/// </para>
/// </summary>
public sealed record EmployeeDto(
    Guid Id,
    string FullName,
    string? Phone,
    string Position,
    decimal MonthlySalary,
    bool IsActive,
    string? Note,
    DateTime CreatedAt);
