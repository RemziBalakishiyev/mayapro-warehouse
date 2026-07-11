using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// Tests the business-zone date logic. A fixed UTC clock + a fixed +4 zone make the day-boundary
/// behaviour deterministic (Baku is UTC+4 with no DST).
/// </summary>
public sealed class AppDateProviderTests
{
    private static readonly TimeZoneInfo Baku =
        TimeZoneInfo.CreateCustomTimeZone("test-baku", TimeSpan.FromHours(4), "Baku", "Baku");

    private static AppDateProvider ProviderAt(DateTime utcNow) => new(Baku, () => utcNow);

    [Fact]
    public void Today_At_2030_Utc_Is_Next_Day_In_Baku()
    {
        // 20:30 UTC = 00:30 the next day in Baku → Today rolls over.
        AppDateProvider provider = ProviderAt(new DateTime(2026, 7, 11, 20, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 7, 12), provider.Today);
    }

    [Fact]
    public void Today_At_1930_Utc_Is_Same_Day_In_Baku()
    {
        // 19:30 UTC = 23:30 same day in Baku → still yesterday's boundary.
        AppDateProvider provider = ProviderAt(new DateTime(2026, 7, 11, 19, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 7, 11), provider.Today);
    }

    [Fact]
    public void ToLocalDate_Maps_Post_Midnight_Utc_To_Local_Day()
    {
        AppDateProvider provider = ProviderAt(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 7, 12), provider.ToLocalDate(new DateTime(2026, 7, 11, 20, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 7, 11), provider.ToLocalDate(new DateTime(2026, 7, 11, 19, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void LocalDayRangeUtc_Is_The_Previous_2000_To_2000_Window()
    {
        AppDateProvider provider = ProviderAt(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        (DateTime start, DateTime end) = provider.LocalDayRangeUtc(new DateOnly(2026, 7, 12));

        Assert.Equal(new DateTime(2026, 7, 11, 20, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 7, 12, 20, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void ResolveTimeZone_Baku_Is_Utc_Plus_4()
    {
        TimeZoneInfo tz = AppDateProvider.ResolveTimeZone("Asia/Baku");

        Assert.Equal(TimeSpan.FromHours(4), tz.GetUtcOffset(new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ResolveTimeZone_Defaults_To_Baku_When_Unset()
    {
        Assert.Equal(TimeSpan.FromHours(4),
            AppDateProvider.ResolveTimeZone(null).GetUtcOffset(new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc)));
    }
}
