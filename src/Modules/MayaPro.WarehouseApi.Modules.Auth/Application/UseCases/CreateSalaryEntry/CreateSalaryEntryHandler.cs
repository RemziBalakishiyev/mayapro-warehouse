using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;

/// <summary>
/// Records one line on an employee's salary account: a <c>payment</c> handed over (salary or advance) or a
/// <c>deduction</c> charged against it. The entry and its activity log are written in a single shared
/// transaction, so neither can survive without the other.
/// <para>
/// The two dates are set from the same clock but mean different things (ADR-0005 / AC4): <c>Date</c> is the
/// instant the cash moved — a payment therefore lands in today's day-end and dashboard figures — while
/// <c>Month</c> is the accounting month it settles and defaults to the current business month when omitted.
/// A deduction moves no cash at all and never reaches those figures.
/// </para>
/// </summary>
public sealed class CreateSalaryEntryHandler(
    IAuthDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CreateSalaryEntryCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result<SalaryEntryDto>> Handle(CreateSalaryEntryCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<SalaryEntryDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        if (!SalaryEntryTypeCode.TryParse(command.Type, out SalaryEntryType type))
            return Result.Failure<SalaryEntryDto>(SalaryErrors.InvalidType);

        // One clock for the whole use case: the omitted month defaults to the business month of the same
        // "now" the entry's date is stamped with.
        string month;
        if (string.IsNullOrWhiteSpace(command.Month))
            month = SalaryMonth.From(dateProvider.Today);
        else if (!SalaryMonth.TryParse(command.Month, out month))
            return Result.Failure<SalaryEntryDto>(SalaryErrors.InvalidMonth);

        User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (user is null)
            return Result.Failure<SalaryEntryDto>(AuthErrors.UserNotFound);

        var entry = SalaryEntry.Create(
            user.Id,
            type,
            command.Amount,
            command.Note,
            dateProvider.UtcNow,
            month,
            currentUser.UserId);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.SalaryEntries.Add(entry);

        await activityLogger.LogAsync(
            "Maaş əməliyyatı",
            $"{user.FullName} — {entry.Amount:0.00} AZN {ActionWord(type)}",
            currentUser.UserId,
            ct);

        await tx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(entry.ToDto());
    }

    private static string ActionWord(SalaryEntryType type) =>
        type == SalaryEntryType.Payment ? "ödəniş verildi" : "tutuldu";
}
