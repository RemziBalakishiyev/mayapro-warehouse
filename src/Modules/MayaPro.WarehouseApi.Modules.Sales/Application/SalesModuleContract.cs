using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.DeleteSale;
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
internal sealed class SalesModuleContract(
    ISalesDbContext db,
    IDateProvider dateProvider,
    DeleteSaleHandler deleteSale) : ISalesModule
{
    public async Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        (DateTime start, DateTime end) = dateProvider.LocalDayRangeUtc(date);

        // BE#15 — qismən ödənişli satış: Cash/Card are no longer "TotalAmount of sales whose PaymentType is
        // Cash/Card" — they are the REAL amount received via each method, which also picks up a Nisyə sale's
        // cash/card down-payment (PaidVia). Credit is only what remains unpaid (TotalAmount − PaidAmount),
        // which by construction is positive on every stored Credit row (SalePaymentPlan never stores Credit
        // with a zero remaining balance).
        // One grouped round trip (conditional sums) rather than three passes over the same day's rows — the
        // three figures then also come from a single consistent snapshot.
        SalesDayTotals? totals = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .GroupBy(_ => 1)
            .Select(g => new SalesDayTotals(
                g.Sum(s => s.PaidVia == PaymentType.Cash ? s.PaidAmount : 0m),
                g.Sum(s => s.PaidVia == PaymentType.Card ? s.PaidAmount : 0m),
                g.Sum(s => s.PaymentType == PaymentType.Credit ? s.TotalAmount - s.PaidAmount : 0m)))
            .FirstOrDefaultAsync(cancellationToken);

        // No sales that day → no group at all.
        return totals ?? new SalesDayTotals(0m, 0m, 0m);
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
                s.Quantity,
                s.UnitPrice,
                s.IsManual,
                s.PaidAmount,
                s.PaidVia.ToCode()))
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

    public async Task<IReadOnlyList<CustomerPurchaseStats>> GetPurchaseStatsByCustomerAsync(
        CancellationToken cancellationToken = default)
    {
        // One grouped query over every sale that carries a customer, whatever the payment type.
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CustomerId != null)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new
            {
                CustomerId = g.Key,
                Last = g.Max(s => s.Date),
                Total = g.Sum(s => s.TotalAmount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerPurchaseStats(r.CustomerId, r.Last, r.Total, r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerSaleEntry>> GetSalesByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        // ToCode() cannot translate to SQL, so project the raw enum and map in memory.
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderBy(s => s.Date)
            .Select(s => new { s.Id, s.Date, s.ProductName, s.Quantity, s.TotalAmount, s.PaymentType })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerSaleEntry(
                r.Id, r.Date, r.ProductName, r.Quantity, r.TotalAmount, r.PaymentType.ToCode()))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerOutstandingSale>> GetOutstandingSalesAsync(
        CancellationToken cancellationToken = default)
    {
        // One round trip over every customer's still-owing sales. RemainingAmount is a computed property
        // (not a column), so the remaining balance is expressed with the mapped fields to stay translatable.
        // The Id tiebreak makes the order deterministic across executions when two sales share a Date (a
        // plain ORDER BY on a non-unique column is not guaranteed stable by the database).
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CustomerId != null && s.TotalAmount - s.PaidAmount > 0m)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                CustomerId = s.CustomerId!.Value,
                s.Date,
                s.ProductName,
                s.Quantity,
                Remaining = s.TotalAmount - s.PaidAmount
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerOutstandingSale(
                r.Id, r.CustomerId, r.Date, r.ProductName, r.Quantity, r.Remaining))
            .ToList();
    }

    public async Task<Result> DeleteCreditSaleAsync(
        Guid saleId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        Sale? sale = await db.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale is null || sale.CustomerId != customerId || sale.PaymentType != PaymentType.Credit)
            return Result.Failure(SaleErrors.NotFound);

        return await deleteSale.Handle(saleId, cancellationToken);
    }

    public async Task<SaleInvoiceInfo?> GetInvoiceSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken = default)
    {
        Sale? sale = await db.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale is null)
            return null;

        return new SaleInvoiceInfo(
            sale.Id,
            sale.Date,
            sale.ProductName,
            sale.Category,
            sale.Quantity,
            sale.UnitPrice,
            sale.Subtotal,
            sale.TotalAmount,
            sale.PaymentType.ToCode(),
            sale.CustomerId,
            sale.PaidAmount);
    }

    public async Task<Guid?> GetSaleIdByInvoiceTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await db.Sales
            .AsNoTracking()
            .Where(s => s.InvoiceToken == token)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
