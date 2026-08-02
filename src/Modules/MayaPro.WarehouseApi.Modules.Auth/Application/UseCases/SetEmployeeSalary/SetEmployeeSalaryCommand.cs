namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;

/// <summary>
/// Sets an employee's agreed monthly salary. <c>Id</c> comes from the route, so the request body carries
/// only <c>monthlySalary</c>.
/// </summary>
public sealed record SetEmployeeSalaryCommand(Guid Id, decimal MonthlySalary);
