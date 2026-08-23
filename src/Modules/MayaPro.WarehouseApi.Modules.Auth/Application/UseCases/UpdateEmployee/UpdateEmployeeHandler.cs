using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;

/// <summary>
/// Edits a payroll record and logs the change in one transaction (BE#57). The employee's salary history is
/// untouched: renaming someone or correcting their job title never rewrites what they were already paid.
/// <para>
/// The row is loaded through the tenant-filtered set, so another shop's id is simply not found — a 404, not
/// a 403, because the caller must not learn that the id exists at all.
/// </para>
/// </summary>
public sealed class UpdateEmployeeHandler(
    IAuthDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<UpdateEmployeeCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<EmployeeDto>> Handle(UpdateEmployeeCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<EmployeeDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // Checked before the row is loaded, so a bad phone cannot leave the record half-edited.
        Result<string?> phone = PhoneNormalizer.NormalizeOptional(command.Phone);
        if (phone.IsFailure)
            return Result.Failure<EmployeeDto>(phone.Error);

        Employee? employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (employee is null)
            return Result.Failure<EmployeeDto>(EmployeeErrors.NotFound);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        employee.Update(
            command.FullName.Trim(),
            phone.Value,
            command.Position.Trim(),
            command.MonthlySalary,
            command.Note);

        await activityLogger.LogAsync(
            "İşçini düzəltdi",
            $"{employee.FullName} — {employee.Position}",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(employee.ToDto());
    }
}
