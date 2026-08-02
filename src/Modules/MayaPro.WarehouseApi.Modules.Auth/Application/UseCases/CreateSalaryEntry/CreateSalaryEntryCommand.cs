namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;

/// <summary>
/// Adds a line to an employee's salary account. <c>UserId</c> comes from the route. <c>Type</c> is the wire
/// code ("payment" | "deduction"); <c>Month</c> (<c>yyyy-MM</c>) is optional and defaults to the current
/// business month.
/// </summary>
public sealed record CreateSalaryEntryCommand(
    Guid UserId,
    string Type,
    decimal Amount,
    string? Note,
    string? Month);
