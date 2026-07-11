namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;

/// <summary>
/// Resolves a period code ("today" | "week" | "month") to an inclusive date range ending today.
/// Anything unrecognised falls back to today, so the endpoint always returns a sensible window.
/// </summary>
public readonly record struct ReportPeriod(string Code, DateOnly From, DateOnly To)
{
    public const string Today = "today";
    public const string Week = "week";
    public const string Month = "month";

    public static ReportPeriod Resolve(string? period, DateOnly today)
    {
        string code = (period ?? Today).Trim().ToLowerInvariant();
        return code switch
        {
            Week => new ReportPeriod(Week, today.AddDays(-6), today),
            Month => new ReportPeriod(Month, new DateOnly(today.Year, today.Month, 1), today),
            _ => new ReportPeriod(Today, today, today)
        };
    }
}
