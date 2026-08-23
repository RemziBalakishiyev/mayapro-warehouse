using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateEmployee;

/// <summary>
/// Adds a person to the payroll register (BE#57). No account is created and no password is asked for — the
/// whole point of the split is that the shop can pay a labourer without first inventing a login for him.
/// <para>
/// Two employees may share a phone number, and that is not a conflict: unlike <c>Users.Phone</c> the column
/// carries no unique index, so no 409 exists on this route. The row and its activity log commit together.
/// </para>
/// </summary>
public sealed class CreateEmployeeHandler(
    IAuthDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CreateEmployeeCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<EmployeeDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        // BE#46 — optional, so blank lands as a genuine NULL; present but unreadable is a 400 before
        // anything is written. Stored canonically so a wa.me link needs no further cleaning.
        Result<string?> phone = PhoneNormalizer.NormalizeOptional(command.Phone);
        if (phone.IsFailure)
            return Result.Failure<EmployeeDto>(phone.Error);

        var employee = Employee.Create(
            command.FullName.Trim(),
            phone.Value,
            command.Position.Trim(),
            command.MonthlySalary,
            command.Note);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Employees.Add(employee);

        await activityLogger.LogAsync(
            "İşçi əlavə etdi",
            $"{employee.FullName} — {employee.Position}",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(employee.ToDto());
    }
}
