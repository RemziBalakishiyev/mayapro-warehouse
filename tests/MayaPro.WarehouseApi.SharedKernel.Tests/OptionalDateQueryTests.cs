using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// BE#31 — the shared "optional from/to query-string date" parser used by ReportsEndpoints and
/// ExportsEndpoints: an absent/blank value is valid ("unbounded"), an unparsable one is reported via the
/// out error instead of throwing, so callers can turn it into a normal { code, message } 400.
/// </summary>
public sealed class OptionalDateQueryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_Empty_Or_Whitespace_Is_Valid_And_Unbounded(string? raw)
    {
        bool ok = OptionalDateQuery.TryParse(raw, out DateOnly? date, out string? error);

        Assert.True(ok);
        Assert.Null(date);
        Assert.Null(error);
    }

    [Fact]
    public void A_Valid_Iso_Date_Is_Parsed()
    {
        bool ok = OptionalDateQuery.TryParse("2026-08-02", out DateOnly? date, out string? error);

        Assert.True(ok);
        Assert.Equal(new DateOnly(2026, 8, 2), date);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-13-40")]
    [InlineData("08/02/2026 garbage")]
    public void An_Unparsable_Value_Fails_With_A_Message_Instead_Of_Throwing(string raw)
    {
        bool ok = OptionalDateQuery.TryParse(raw, out DateOnly? date, out string? error);

        Assert.False(ok);
        Assert.Null(date);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
