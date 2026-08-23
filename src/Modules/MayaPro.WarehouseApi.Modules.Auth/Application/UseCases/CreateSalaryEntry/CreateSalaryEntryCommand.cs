namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;

/// <summary>
/// Adds a line to an employee's salary account. <c>EmployeeId</c> comes from the route (BE#57 — a payroll
/// record, not a login account). <c>Type</c> is the wire code ("payment" | "deduction"); <c>Month</c>
/// (<c>yyyy-MM</c>) is optional and defaults to the current business month.
/// </summary>
public sealed record CreateSalaryEntryCommand(
    Guid EmployeeId,
    string Type,
    decimal Amount,
    string? Note,
    string? Month);
