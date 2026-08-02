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

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == command.Id, ct);
        if (user is null)
            return Result.Failure<EmployeeDto>(AuthErrors.UserNotFound);

        user.SetMonthlySalary(command.MonthlySalary);
        await db.SaveChangesAsync(ct);

        return Result.Success(new EmployeeDto(
            user.Id, user.FullName, user.Phone, user.Role.ToCode(), user.IsActive, user.MonthlySalary));
    }
}
