using FluentValidation;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;

/// <summary>
/// Closes the day. The day's sales (by payment type) and expense total are computed server-side from the
/// Sales/Expenses modules; the client sends only the cash figures. A day can be closed once — a unique
/// index on Date guards against the race, surfaced as the "already closed" business error.
/// <para>
/// BE#28: salary <b>payments</b> made today are cash that left the same drawer, so they are added to the
/// expense total before the closing is built — expected cash then reads
/// <c>opening + cash sales − (expenses + salary payments)</c>. They are folded into the existing
/// <c>Expenses</c> figure (ADR-0006-safe: existing figures never change). Salary <b>deductions</b> move no
/// cash and are deliberately not fetched at all.
/// </para>
/// <para>
/// BE#33: the same salary-payments figure is also stored on <c>Closing.SalaryExpenses</c> — an additive,
/// informational breakdown of <c>Expenses</c> — so the frontend can show "employee payments" as its own
/// line, matching exactly what <c>GetSummaryHandler</c>'s pre-close preview already reported.
/// </para>
/// </summary>
public sealed class CloseDayHandler(
    IDayEndDbContext db,
    IUnitOfWork unitOfWork,
    ISalesModule sales,
    IExpensesModule expenses,
    ISalaryModule salary,
    IValidator<CloseDayCommand> validator,
    IActivityLogger activityLogger,
    ICurrentUser currentUser,
    IDateProvider dateProvider)
{
    public async Task<Result<ClosingDto>> Handle(CloseDayCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<ClosingDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        DateOnly date = dateProvider.Today;

        // Friendly pre-check; the unique index below is the real guard against a concurrent second close.
        if (await db.Closings.AnyAsync(c => c.Date == date, ct))
            return Result.Failure<ClosingDto>(DayEndErrors.AlreadyClosed);

        SalesDayTotals totals = await sales.GetDayTotalsAsync(date, ct);
        decimal expenseTotal = await expenses.GetDayTotalAsync(date, ct);
        decimal salaryPaid = await salary.GetDayPaymentsTotalAsync(date, ct);

        var closing = Closing.Create(
            date,
            command.OpeningCash,
            totals.Cash,
            totals.Card,
            totals.Credit,
            expenseTotal + salaryPaid,
            salaryPaid,
            command.ActualCash,
            currentUser.UserId,
            command.Note);

        await using IUnitOfWorkTransaction tx = await unitOfWork.BeginTransactionAsync(ct);

        db.Closings.Add(closing);

        await activityLogger.LogAsync(
            "Gün sonu bağladı",
            $"{date:yyyy-MM-dd} — fərq: {closing.Difference:0.00} AZN",
            currentUser.UserId,
            ct);

        try
        {
            await tx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request closed the same day first (unique index violation).
            return Result.Failure<ClosingDto>(DayEndErrors.AlreadyClosed);
        }

        await tx.CommitAsync(ct);

        return Result.Success(closing.ToDto());
    }
}
