using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application;

/// <summary>
/// The Sales module's implementation of <see cref="ISalesModule"/>: day totals for day-end and rows for
/// reports. All day boundaries are the business time zone's (via <see cref="IDateProvider"/>), so a sale
/// just after Baku midnight belongs to the Baku day even though it is still "yesterday" in UTC.
/// </summary>
internal sealed class SalesModuleContract(ISalesDbContext db, IDateProvider dateProvider) : ISalesModule
{
    public async Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        (DateTime start, DateTime end) = dateProvider.LocalDayRangeUtc(date);

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
            query = query.Where(s => s.Date >= dateProvider.LocalDayRangeUtc(f).StartUtc);
        if (to is { } t)
            query = query.Where(s => s.Date < dateProvider.LocalDayRangeUtc(t).EndUtc);

        List<Sale> sales = await query.OrderBy(s => s.Date).ToListAsync(cancellationToken);

        return sales
            .Select(s => new SalesReportRow(
                dateProvider.ToLocalDate(s.Date),
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
        // Free-form sales have no product, so they can't be "frozen stock" — exclude them from the grouping.
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.ProductId != null)
            .GroupBy(s => s.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Last = g.Max(s => s.Date) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ProductLastSale(r.ProductId, dateProvider.ToLocalDate(r.Last)))
            .ToList();
    }

    public async Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        List<Sale> sales = await db.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.Date)
            .Take(take)
            .ToListAsync(cancellationToken);

        return sales
            .Select(s => new RecentSaleInfo(
                s.Id,
                dateProvider.ToLocalDate(s.Date),
                s.ProductName,
                s.Category,
                s.Quantity,
                s.TotalAmount,
                s.PaymentType.ToCode(),
                s.CustomerId))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerLastPurchase>> GetLastCreditSaleDatesByCustomerAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CustomerId != null)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Last = g.Max(s => s.Date) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerLastPurchase(r.CustomerId, r.Last))
            .ToList();
    }
}
