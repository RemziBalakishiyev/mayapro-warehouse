using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// The "an expense date cannot be in the future" rule (BE#9). A fixed +4 zone (Baku has no DST) plus a fixed
/// clock make the day boundary deterministic — the point being that "today" comes from
/// <see cref="MayaPro.WarehouseApi.SharedKernel.Application.IDateProvider"/> (Asia/Baku, ADR-0005), not from a
/// naive UTC-calendar comparison, which would misjudge the four hours on either side of the local midnight.
/// </summary>
public sealed class CreateExpenseValidatorTests
{
    private const string FutureDateMessage = "Xərcin tarixi gələcək ola bilməz";

    private static readonly TimeZoneInfo Baku =
        TimeZoneInfo.CreateCustomTimeZone("test-baku", TimeSpan.FromHours(4), "Baku", "Baku");

    // 2026-07-27 14:00 Baku (= 10:00 UTC) — a plain daytime instant, well clear of the day boundary.
    private static readonly DateTime DaytimeUtcNow = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private static CreateExpenseValidator ValidatorAt(DateTime utcNow) => new(new AppDateProvider(Baku, () => utcNow));

    private static CreateExpenseCommand CommandWithDate(DateTime? date) =>
        new("Karqo", "Yol", "general", 100m, date, null, null);

    [Fact]
    public void Tomorrows_Date_Is_Invalid()
    {
        // TC-01.
        var result = ValidatorAt(DaytimeUtcNow).Validate(CommandWithDate(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == FutureDateMessage);
    }

    [Fact]
    public void Todays_Date_Passes()
    {
        // TC-02.
        Assert.True(ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc))).IsValid);
    }

    [Fact]
    public void Past_Date_Passes()
    {
        // TC-03.
        Assert.True(ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))).IsValid);
    }

    [Fact]
    public void Omitted_Date_Passes()
    {
        // No date → the handler stamps "now", so there is nothing to reject.
        Assert.True(ValidatorAt(DaytimeUtcNow).Validate(CommandWithDate(null)).IsValid);
    }

    [Fact]
    public void Todays_Date_Passes_During_The_Baku_Early_Morning()
    {
        // TC-04: "now" is 01:30 Baku on the 27th, which is still the evening of the 26th in UTC
        // (2026-07-26 21:30Z). Today's Baku date (the 27th) looks like "tomorrow" on the UTC calendar,
        // so a UTC-based rule would wrongly reject it.
        var result = ValidatorAt(new DateTime(2026, 7, 26, 21, 30, 0, DateTimeKind.Utc))
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Instant_That_Is_Already_Tomorrow_In_Baku_Is_Invalid()
    {
        // The mirror of TC-04: 2026-07-27 20:00Z is the same UTC day as "now", but 00:00 on the 28th in
        // Baku — a future business day, so it must be rejected even though the UTC dates match.
        var result = ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 27, 20, 0, 0, DateTimeKind.Utc)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == FutureDateMessage);
    }

    [Fact]
    public void Future_Date_Without_A_Kind_Is_Invalid()
    {
        // A body of "2026-07-28T00:00:00" (no offset) deserializes to DateTimeKind.Unspecified; the rule must
        // still catch it rather than throw or silently pass.
        var result = ValidatorAt(DaytimeUtcNow)
            .Validate(CommandWithDate(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Unspecified)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == FutureDateMessage);
    }
}
