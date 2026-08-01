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

    /// <summary>
    /// An expense type with the same name already exists. Code deliberately does not end in
    /// <c>AlreadyExists</c>/<c>Conflict</c> so the shared Result→HTTP convention maps it to 400 (the agreed
    /// behaviour for a duplicate type), not 409.
    /// </summary>
    public static readonly Error ExpenseTypeDuplicate =
        new("Expenses.ExpenseTypeDuplicate", "Bu xərc növü artıq mövcuddur");

    /// <summary>An unrecognised <c>source</c> query filter (must be "general" or "product").</summary>
    public static readonly Error InvalidSource =
        new("Expenses.InvalidSource", "Yanlış xərc mənbəyi");

    /// <summary>
    /// A <c>from</c>/<c>to</c> query filter that is malformed (not <c>yyyy-MM-dd</c>) or where
    /// <c>from</c> is later than <c>to</c>.
    /// </summary>
    public static readonly Error InvalidDateRange =
        new("Expenses.InvalidDateRange", "Yanlış tarix aralığı");
}
