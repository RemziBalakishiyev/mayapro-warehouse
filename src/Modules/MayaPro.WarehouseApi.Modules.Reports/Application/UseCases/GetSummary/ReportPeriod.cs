namespace MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSummary;

/// <summary>
/// A report period resolved to an inclusive date range ending today. "all" spans the whole history
/// (no bounds). An absent period defaults to today; an unrecognised one is rejected by the caller rather
/// than silently coerced — a wrong window would show misleading numbers.
/// </summary>
public readonly record struct ReportPeriod(string Code, DateOnly? From, DateOnly? To)
{
    public const string Today = "today";
    public const string Week = "week";
    public const string Month = "month";
    public const string All = "all";

    public static bool TryResolve(string? period, DateOnly today, out ReportPeriod result)
    {
        // Absent → today; otherwise the value must be one we recognise.
        string code = string.IsNullOrWhiteSpace(period) ? Today : period.Trim().ToLowerInvariant();
        switch (code)
        {
            case Today:
                result = new ReportPeriod(Today, today, today);
                return true;
            case Week:
                result = new ReportPeriod(Week, today.AddDays(-6), today);
                return true;
            case Month:
                result = new ReportPeriod(Month, new DateOnly(today.Year, today.Month, 1), today);
                return true;
            case All:
                result = new ReportPeriod(All, null, null);
                return true;
            default:
                result = default;
                return false;
        }
    }
}
