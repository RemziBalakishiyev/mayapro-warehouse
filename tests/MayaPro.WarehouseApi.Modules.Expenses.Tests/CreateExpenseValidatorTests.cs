using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpense;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Tests the "expense date cannot be in the future" rule (BE#9). A fixed +4 zone (Baku has no DST) makes
/// the day-boundary behaviour deterministic — in particular that "today" is computed in Asia/Baku via
/// <see cref="MayaPro.WarehouseApi.SharedKernel.Application.IDateProvider"/>, not by a naive UTC-calendar
/// comparison.
/// </summary>
public sealed class CreateExpenseValidatorTests
{
    private static readonly TimeZoneInfo Baku =
        TimeZoneInfo.CreateCustomTimeZone("test-baku", TimeSpan.FromHours(4), "Baku", "Baku");

    // 2026-07-27 14:00 Baku (= 10:00 UTC) — a plain daytime instant, well clear of the day boundary.
    private static readonly DateTime DaytimeUtcNow = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private static CreateExpenseValidator ValidatorAt(DateTime utcNow) => new(new AppDateProvider(Baku, () => utcNow));

    private static CreateExpenseCommand CommandWithDate(DateTime? date) =>
        new("Karqo", "Yol", 100m, date, null, null);

    [Fact]
    public async Task Tomorrow_Date_Fails_Validation()
    {
        // TC-01: sabahkı tarix rədd olunmalıdır.
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Xərcin tarixi gələcək ola bilməz");
    }

    [Fact]
    public async Task Todays_Date_Passes_Validation()
    {
        // TC-02: bugünkü tarix keçməlidir.
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Past_Date_Passes_Validation()
    {
        // TC-03: keçmiş tarix keçməlidir.
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Null_Date_Passes_Validation()
    {
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Baku_Early_Morning_Todays_Date_Is_Not_Rejected_By_Utc_Drift()
    {
        // TC-04: "indiki an" Bakı vaxtı ilə 01:30-dır (27-si) — bu, UTC-də hələ 26-sının axşamıdır
        // (2026-07-26 21:30 UTC). Bugünkü tarix (Bakı təqvimi ilə 27-si) UTC təqviminə görə "sabah"
        // kimi görünsə də, IDateProvider-ə görə (Bakı) bu, elə bugündür və rədd olunmamalıdır.
        var earlyMorningBaku = new DateTime(2026, 7, 26, 21, 30, 0, DateTimeKind.Utc);
        var validator = ValidatorAt(earlyMorningBaku);
        var command = CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
