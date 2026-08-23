using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;

/// <summary>
/// Sets an employee's agreed monthly salary (owner only). The figure is the baseline every month's salary
/// summary measures payments and deductions against; it is not itself a money movement, so nothing is
/// booked to the cash drawer here.
/// </summary>
public sealed class SetEmployeeSalaryHandler(
    IAuthDbContext db,
    IValidator<SetEmployeeSalaryCommand> validator)
{
    public async Task<Result<EmployeeDto>> Handle(SetEmployeeSalaryCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<EmployeeDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // BE#57 — the salary belongs to the payroll record, not to a login account.
        Employee? employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (employee is null)
            return Result.Failure<EmployeeDto>(EmployeeErrors.NotFound);

        employee.SetMonthlySalary(command.MonthlySalary);
        await db.SaveChangesAsync(ct);

        return Result.Success(employee.ToDto());
    }
}
