using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Expenses.Domain;

/// <summary>Business errors for the Expenses module. Messages are user-facing (Azerbaijani).</summary>
public static class ExpenseErrors
{
    public static readonly Error NotFound =
        new("Expenses.NotFound", "Xərc tapılmadı");

    /// <summary>
    /// The expense's day has been closed, so it can no longer be edited or deleted. Code ends in
    /// <c>Conflict</c> so the shared Result→HTTP convention maps it to 409.
    /// </summary>
    public static readonly Error DayClosedConflict =
        new("Expenses.DayClosedConflict", "Bu günün hesabı bağlanıb — xərcə dəyişiklik etmək olmaz");
}
