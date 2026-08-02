using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.DeleteSalaryEntry;

/// <summary>
/// Removes a line from an employee's salary account (owner only), together with its activity log in one
/// shared transaction. The entry is looked up by <b>both</b> ids: an entry that belongs to another employee
/// is "not found" here, so a wrong route can never delete — or even reveal — someone else's line.
/// </summary>
public sealed class DeleteSalaryEntryHandler(
    IAuthDbContext db,
    IUnitOfWork unitOfWork,
    IActivityLogger activityLogger,
    ICurrentUser currentUser)
{
    public async Task<Result> Handle(Guid userId, Guid entryId, CancellationToken ct)
    {
        SalaryEntry? entry = await db.SalaryEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, ct);
        if (entry is null)
            return Result.Failure(SalaryErrors.EntryNotFound);

        string fullName = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.SalaryEntries.Remove(entry);

        await activityLogger.LogAsync(
            "Maaş əməliyyatını sildi",
            $"{fullName} — {entry.Amount:0.00} AZN",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }
}
