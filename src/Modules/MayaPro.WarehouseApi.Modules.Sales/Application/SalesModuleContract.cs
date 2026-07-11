using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application;

/// <summary>The Sales module's implementation of <see cref="ISalesModule"/>: day totals for day-end.</summary>
internal sealed class SalesModuleContract(ISalesDbContext db) : ISalesModule
{
    public async Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        DateTime start = date.ToDateTime(TimeOnly.MinValue);
        DateTime end = start.AddDays(1);

        var byType = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .GroupBy(s => s.PaymentType)
            .Select(g => new { Type = g.Key, Total = g.Sum(s => s.TotalAmount) })
            .ToListAsync(cancellationToken);

        decimal Total(PaymentType type) => byType.FirstOrDefault(x => x.Type == type)?.Total ?? 0m;

        return new SalesDayTotals(Total(PaymentType.Cash), Total(PaymentType.Card), Total(PaymentType.Credit));
    }

    public async Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Sale> query = db.Sales.AsNoTracking();

        if (from is { } f)
            query = query.Where(s => s.Date >= f.ToDateTime(TimeOnly.MinValue));
        if (to is { } t)
            query = query.Where(s => s.Date < t.AddDays(1).ToDateTime(TimeOnly.MinValue));

        List<Sale> sales = await query.OrderBy(s => s.Date).ToListAsync(cancellationToken);

        return sales
            .Select(s => new SalesReportRow(
                DateOnly.FromDateTime(s.Date),
                s.TotalAmount,
                s.Profit,
                s.PaymentType.ToCode(),
                s.ProductId,
                s.ProductName,
                s.Quantity))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Sales
            .AsNoTracking()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Last = g.Max(s => s.Date) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ProductLastSale(r.ProductId, DateOnly.FromDateTime(r.Last)))
            .ToList();
    }
}
