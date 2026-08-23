namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>
/// One employee's salary standing for a month: what was agreed, what was paid, what was deducted and what
/// is left. <c>Remaining = MonthlySalary − PaidTotal − DeductionTotal</c> and may be negative — that simply
/// means the employee has already been paid more than the month owes.
/// <para>
/// BE#57 (breaking): <c>userId</c> became <c>employeeId</c> and <c>role</c> became <c>position</c>. The row
/// identifies a payroll record, not a login account.
/// </para>
/// </summary>
public sealed record EmployeeSalarySummaryDto(
    Guid EmployeeId,
    string FullName,
    string Position,
    decimal MonthlySalary,
    decimal PaidTotal,
    decimal DeductionTotal,
    decimal Remaining);
