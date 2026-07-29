using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Editing an expense obeys the same "date cannot be in the future" rule as creating one (AC-2). Same fixed
/// +4 zone (Baku, no DST) and fixed clock as <see cref="CreateExpenseValidatorTests"/>.
/// </summary>
public sealed class UpdateExpenseValidatorTests
{
    private const string FutureDateMessage = "Xərcin tarixi gələcək ola bilməz";

    private static readonly TimeZoneInfo Baku =
        TimeZoneInfo.CreateCustomTimeZone("test-baku", TimeSpan.FromHours(4), "Baku", "Baku");

    // 2026-07-27 14:00 Baku (= 10:00 UTC) — a plain daytime instant, well clear of the day boundary.
    private static readonly DateTime DaytimeUtcNow = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private static UpdateExpenseValidator ValidatorAt(DateTime utcNow) => new(new AppDateProvider(Baku, () => utcNow));

    private static UpdateExpenseCommand CommandWithDate(DateTime? date) =>
        new(Guid.NewGuid(), "Karqo (düzəliş)", "Yol", "general", 100m, date, null, null);

    [Fact]
    public void Tomorrows_Date_Is_Invalid()
    {
        var result = ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == FutureDateMessage);
    }

    [Fact]
    public void Todays_Date_Passes()
    {
        Assert.True(ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc))).IsValid);
    }

    [Fact]
    public void Past_Date_Passes()
    {
        Assert.True(ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))).IsValid);
    }

    [Fact]
    public void Omitted_Date_Passes_And_Keeps_The_Existing_Date()
    {
        // Date omitted → the handler keeps the expense's current date, so the rule must not fire on null.
        Assert.True(ValidatorAt(DaytimeUtcNow).Validate(CommandWithDate(null)).IsValid);
    }

    [Fact]
    public void Todays_Date_Passes_During_The_Baku_Early_Morning()
    {
        // Editing at 01:30 Baku on the 27th (2026-07-26 21:30Z): today's Baku date must not be rejected just
        // because the UTC calendar is still on the 26th.
        var result = ValidatorAt(new DateTime(2026, 7, 26, 21, 30, 0, DateTimeKind.Utc))
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Instant_That_Is_Already_Tomorrow_In_Baku_Is_Invalid()
    {
        // 2026-07-27 20:00Z is the same UTC day as "now" but 00:00 on the 28th in Baku → a future business day.
        var result = ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 20, 0, 0, DateTimeKind.Utc)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == FutureDateMessage);
    }
}
