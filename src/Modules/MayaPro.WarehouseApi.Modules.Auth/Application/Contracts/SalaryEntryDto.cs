namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>
/// One line of an employee's salary account on the wire. <c>Type</c> is the frozen wire code
/// ("payment" | "deduction"); <c>Date</c> is when the cash moved and <c>Month</c> (<c>yyyy-MM</c>) is the
/// accounting month it settles.
/// <para>
/// BE#57 (breaking): <c>userId</c> became <c>employeeId</c> — who was paid. <c>createdByUserId</c> is
/// unchanged and still names the login account that recorded the line — who paid.
/// </para>
/// </summary>
public sealed record SalaryEntryDto(
    Guid Id,
    Guid EmployeeId,
    string Type,
    decimal Amount,
    string? Note,
    DateTime Date,
    string Month,
    Guid? CreatedByUserId,
    DateTime CreatedAt);
