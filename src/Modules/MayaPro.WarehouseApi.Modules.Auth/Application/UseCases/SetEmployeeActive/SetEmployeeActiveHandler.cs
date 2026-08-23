using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeActive;

/// <summary>
/// Retires an employee from the payroll, or brings them back (BE#57). This is what replaces deletion: there
/// is no <c>DELETE /api/employees/{id}</c>, because removing the row would erase a salary history the shop
/// is required to keep — and would leave the money that really did leave the drawer pointing at nothing.
/// <para>
/// Idempotent by design: deactivating an already-inactive employee is a 200, not a 409. The caller is
/// stating a desired end state, and the end state is already true.
/// </para>
/// </summary>
public sealed class SetEmployeeActiveHandler(
    IAuthDbContext db,
    IUnitOfWork unitOfWork,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result<EmployeeDto>> Handle(Guid id, bool isActive, CancellationToken ct)
    {
        Employee? employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null)
            return Result.Failure<EmployeeDto>(EmployeeErrors.NotFound);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        if (isActive)
            employee.Activate();
        else
            employee.Deactivate();

        await activityLogger.LogAsync(
            isActive ? "İşçini aktiv etdi" : "İşçini deaktiv etdi",
            employee.FullName,
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(employee.ToDto());
    }
}
