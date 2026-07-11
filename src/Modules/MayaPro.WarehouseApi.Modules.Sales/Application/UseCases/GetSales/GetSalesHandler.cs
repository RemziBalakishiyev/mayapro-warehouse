using System.Globalization;
using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.GetSales;

/// <summary>
/// Returns sales, newest first. With a <c>date</c> (ISO <c>yyyy-MM-dd</c>) only that day's sales are
/// returned; without it, all sales.
/// </summary>
public sealed class GetSalesHandler(ISalesDbContext db)
{
    public async Task<IReadOnlyList<SaleDto>> Handle(string? date, CancellationToken ct)
    {
        IQueryable<Sale> query = db.Sales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(date)
            && DateOnly.TryParse(date, CultureInfo.InvariantCulture, out DateOnly day))
        {
            DateTime start = day.ToDateTime(TimeOnly.MinValue);
            DateTime end = start.AddDays(1);
            query = query.Where(s => s.Date >= start && s.Date < end);
        }

        List<Sale> sales = await query
            .OrderByDescending(s => s.Date)
            .ToListAsync(ct);

        return sales.Select(s => s.ToDto()).ToList();
    }
}
