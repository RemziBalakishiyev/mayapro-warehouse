namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;

/// <summary>
/// Edits a payroll record (BE#57). <c>Id</c> comes from the route; <c>IsActive</c> is not editable here —
/// activating and deactivating are their own explicit routes.
/// </summary>
public sealed record UpdateEmployeeCommand(
    Guid Id,
    string FullName,
    string? Phone,
    string Position,
    decimal MonthlySalary = 0m,
    string? Note = null);
