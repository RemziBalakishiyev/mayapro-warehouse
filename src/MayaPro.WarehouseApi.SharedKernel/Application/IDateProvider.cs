namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// Supplies the current date/time in the application's business time zone (Asia/Baku). Everything that
/// reasons about "today" — day-end closing, day totals, the dashboard's daily figures, the sales
/// date filter — goes through this so a sale made at 00:30 Baku (20:30 UTC) counts against the Baku day,
/// not the UTC day.
/// </summary>
public interface IDateProvider
{
    /// <summary>The current instant in UTC.</summary>
    DateTime UtcNow { get; }

    /// <summary>Today's date in the business time zone.</summary>
    DateOnly Today { get; }

    /// <summary>Converts a UTC instant to its date in the business time zone.</summary>
    DateOnly ToLocalDate(DateTime utc);

    /// <summary>
    /// The half-open UTC window <c>[start, end)</c> that corresponds to a whole day in the business time
    /// zone. Use it to filter UTC-stored timestamps for "that local day".
    /// </summary>
    (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate);
}
