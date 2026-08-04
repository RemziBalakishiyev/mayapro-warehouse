namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// Shared "optional <c>from</c>/<c>to</c> query-string date" parsing for endpoints that bind the value as
/// a raw <see cref="string"/> rather than <see cref="DateOnly"/>? — binding it as <c>DateOnly?</c> directly
/// lets minimal-API's model binding throw on an unparsable value, which the <c>GlobalExceptionHandler</c>
/// then turns into a 500 instead of the usual <c>{ code, message }</c> 400. An absent/blank value is valid
/// (it means "unbounded"); only a non-blank, unparsable value is reported as an error.
/// </summary>
public static class OptionalDateQuery
{
    public static bool TryParse(string? raw, out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (DateOnly.TryParse(raw, out DateOnly parsed))
        {
            date = parsed;
            return true;
        }

        error = "Tarix formatı yanlışdır (gözlənilən: yyyy-MM-dd)";
        return false;
    }
}
