using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.UpdateExpense;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Tests that the "expense date cannot be in the future" rule (BE#9) also applies to editing an existing
/// expense — AC #2. Same fixed +4 (Baku, no DST) zone as <see cref="CreateExpenseValidatorTests"/>.
/// </summary>
public sealed class UpdateExpenseValidatorTests
{
    private static readonly TimeZoneInfo Baku =
        TimeZoneInfo.CreateCustomTimeZone("test-baku", TimeSpan.FromHours(4), "Baku", "Baku");

    // 2026-07-27 14:00 Baku (= 10:00 UTC) — a plain daytime instant, well clear of the day boundary.
    private static readonly DateTime DaytimeUtcNow = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private static UpdateExpenseValidator ValidatorAt(DateTime utcNow) => new(new AppDateProvider(Baku, () => utcNow));

    private static UpdateExpenseCommand CommandWithDate(DateTime? date) =>
        new(Guid.NewGuid(), "Karqo (düzəliş)", "Yol", 100m, date, null, null);

    [Fact]
    public async Task Tomorrow_Date_Fails_Validation()
    {
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Xərcin tarixi gələcək ola bilməz");
    }

    [Fact]
    public async Task Todays_Date_Passes_Validation()
    {
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Past_Date_Passes_Validation()
    {
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Null_Date_Keeps_Existing_Date_And_Passes_Validation()
    {
        // Date omitted → the handler keeps the expense's current date, so the validator must not reject null.
        var validator = ValidatorAt(DaytimeUtcNow);
        var command = CommandWithDate(null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
