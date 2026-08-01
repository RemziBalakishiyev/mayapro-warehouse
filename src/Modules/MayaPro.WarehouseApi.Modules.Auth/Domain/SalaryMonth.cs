using System.Globalization;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// The <c>yyyy-MM</c> accounting month a salary entry is booked against (BE#28 / AC4). It is deliberately
/// separate from the entry's <c>Date</c>: the date says when the cash moved (day-end / dashboard read it),
/// the month says whose salary it settles (the summary reads it). Parsing is exact and culture-invariant,
/// mirroring <c>GetExpensesHandler</c>'s month filter.
/// </summary>
public static class SalaryMonth
{
    public const string Format = "yyyy-MM";

    /// <summary>The <c>yyyy-MM</c> form of a business-zone date.</summary>
    public static string From(DateOnly date) => date.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>True when <paramref name="month"/> is a real <c>yyyy-MM</c> month ("2026-13" is not).</summary>
    public static bool TryParse(string? month, out string normalized)
    {
        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            normalized = parsed.ToString(Format, CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
