namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateEmployee;

/// <summary>
/// Adds a person to the payroll register (BE#57). There is no password, no e-mail and no role here — an
/// employee does not sign in. <c>Position</c> is free text ("Satıcı", "Fəhlə"), <c>Phone</c> is an optional
/// contact number that need not be unique, and an omitted <c>MonthlySalary</c> means "not agreed yet" (0).
/// </summary>
public sealed record CreateEmployeeCommand(
    string FullName,
    string? Phone,
    string Position,
    decimal MonthlySalary = 0m,
    string? Note = null);
