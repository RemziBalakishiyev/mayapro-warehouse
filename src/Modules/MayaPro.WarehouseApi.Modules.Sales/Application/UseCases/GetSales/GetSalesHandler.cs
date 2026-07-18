using System.Globalization;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSales;

/// <summary>
/// Returns a page of sales, newest first. Filters:
/// <list type="bullet">
///   <item><c>date</c> (ISO <c>yyyy-MM-dd</c>) — that Baku day only (legacy; takes precedence over from/to).</item>
///   <item><c>from</c>/<c>to</c> — inclusive Baku-day range when <c>date</c> is not set.</item>
/// </list>
/// Pagination: <c>take</c> defaults to 50 (max 200), <c>skip</c> defaults to 0.
/// </summary>
public sealed class GetSalesHandler(ISalesDbContext db, IDateProvider dateProvider)
{
    private const int DefaultTake = 50;
    private const int MaxTake = 200;

    public async Task<PagedResult<SaleDto>> Handle(
        string? date,
        string? from,
        string? to,
        int? take,
        int? skip,
        CancellationToken ct)
    {
        int safeTake = take is > 0 and <= MaxTake ? take.Value : DefaultTake;
        int safeSkip = skip is > 0 ? skip.Value : 0;

        IQueryable<Sale> query = db.Sales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(date)
            && DateOnly.TryParse(date, CultureInfo.InvariantCulture, out DateOnly day))
        {
            (DateTime start, DateTime end) = dateProvider.LocalDayRangeUtc(day);
            query = query.Where(s => s.Date >= start && s.Date < end);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(from)
                && DateOnly.TryParse(from, CultureInfo.InvariantCulture, out DateOnly fromDay))
            {
                DateTime start = dateProvider.LocalDayRangeUtc(fromDay).StartUtc;
                query = query.Where(s => s.Date >= start);
            }

            if (!string.IsNullOrWhiteSpace(to)
                && DateOnly.TryParse(to, CultureInfo.InvariantCulture, out DateOnly toDay))
            {
                DateTime end = dateProvider.LocalDayRangeUtc(toDay).EndUtc;
                query = query.Where(s => s.Date < end);
            }
        }

        int total = await query.CountAsync(ct);

        List<Sale> sales = await query
            .OrderByDescending(s => s.Date)
            .Skip(safeSkip)
            .Take(safeTake)
            .ToListAsync(ct);

        return new PagedResult<SaleDto>(sales.Select(s => s.ToDto()).ToList(), total, safeSkip, safeTake);
    }
}
