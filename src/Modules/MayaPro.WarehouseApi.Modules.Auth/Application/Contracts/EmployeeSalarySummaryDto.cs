namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>
/// One employee's salary standing for a month: what was agreed, what was paid, what was deducted and what
/// is left. <c>Remaining = MonthlySalary − PaidTotal − DeductionTotal</c> and may be negative — that simply
/// means the employee has already been paid more than the month owes.
/// </summary>
public sealed record EmployeeSalarySummaryDto(
    Guid UserId,
    string FullName,
    string Role,
    decimal MonthlySalary,
    decimal PaidTotal,
    decimal DeductionTotal,
    decimal Remaining);
