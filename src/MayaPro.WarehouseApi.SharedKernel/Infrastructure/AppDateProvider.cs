using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// <see cref="IDateProvider"/> over a configured time zone. The clock is injected as a delegate so the
/// conversion logic (especially the day-boundary handling) can be unit-tested with a fixed instant.
/// </summary>
public sealed class AppDateProvider(TimeZoneInfo timeZone, Func<DateTime> utcNowProvider) : IDateProvider
{
    public const string DefaultTimeZoneId = "Asia/Baku";

    public DateTime UtcNow => utcNowProvider();

    public DateOnly Today => ToLocalDate(utcNowProvider());

    public DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate)
    {
        // Midnight of the local day (Unspecified kind), converted back to the UTC instant it happened at.
        DateTime localMidnight = localDate.ToDateTime(TimeOnly.MinValue);
        DateTime startUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone);
        return (startUtc, startUtc.AddDays(1));
    }

    /// <summary>Resolves the configured time-zone id, falling back to Asia/Baku when unset.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? id) =>
        TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? DefaultTimeZoneId : id);
}
