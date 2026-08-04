using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// Hand-written doubles for the Exports handlers' collaborators (the solution carries no mocking library
/// on purpose). Only the members the label sheet actually uses are implemented; everything else throws,
/// so an accidental new dependency shows up as a failing test rather than silent behaviour.
/// </summary>
internal sealed class StubProductsModule(params ProductLabelInfo[] products) : IProductsModule
{
    private readonly Dictionary<Guid, ProductLabelInfo> _products = products.ToDictionary(p => p.Id);

    /// <summary>Every id set passed to <see cref="GetLabelInfoAsync"/>, in order — proves it is one call, deduplicated.</summary>
    public List<IReadOnlyCollection<Guid>> LabelLookups { get; } = [];

    public Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        LabelLookups.Add(productIds);
        IReadOnlyList<ProductLabelInfo> found = productIds
            .Where(_products.ContainsKey)
            .Select(id => _products[id])
            .ToList();
        return Task.FromResult(found);
    }

    public Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
        Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> IncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<ProductSnapshot>> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> AddExpenseToProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> RemoveExpenseFromProductAsync(
        Guid productId, string category, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Dictionary<Guid, int>> GetCountBySupplierAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProductExportRow>> GetExportProductsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<StockAdjustmentRow>> GetStockAdjustmentsAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>A frozen clock, so the export file name is asserted against a known date.</summary>
internal sealed class FixedDateProvider(DateOnly today) : IDateProvider
{
    public DateTime UtcNow => today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public DateOnly Today => today;

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

    public DateTime ToLocalDateTime(DateTime utc) => utc;

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
        (localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}

/// <summary>
/// Serves one fixed sale to <see cref="ExportSaleInvoicePdf.ExportSaleInvoicePdfHandler"/> and — via
/// <see cref="ForReport"/> (BE#19) — a fixed set of report rows to
/// <see cref="ExportSalesPdf.ExportSalesPdfHandler"/>. Whichever half a test does not set up keeps throwing,
/// so an accidental new dependency still shows up as a failing test.
/// </summary>
internal sealed class StubSalesModule(
    Guid saleId = default,
    SaleInvoiceInfo? sale = null,
    string? token = null,
    IReadOnlyList<SalesReportRow>? reportRows = null) : ISalesModule
{
    /// <summary>The sales-period handlers' half: a fixed row set, served through the requested window.</summary>
    public static StubSalesModule ForReport(params SalesReportRow[] rows) => new(reportRows: rows);

    public Task<SaleInvoiceInfo?> GetInvoiceSaleAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(id == saleId ? sale : null);

    public Task<Guid?> GetSaleIdByInvoiceTokenAsync(string requestedToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(token is not null && requestedToken == token ? (Guid?)saleId : null);

    public Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        if (reportRows is null)
            throw new NotSupportedException();

        IReadOnlyList<SalesReportRow> window = reportRows
            .Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to))
            .ToList();
        return Task.FromResult(window);
    }

    public Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerPurchaseStats>> GetPurchaseStatsByCustomerAsync(
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerSaleEntry>> GetSalesByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerOutstandingSale>> GetOutstandingSalesAsync(
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result> DeleteCreditSaleAsync(
        Guid saleIdToDelete, Guid customerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>Serves one fixed customer (or none) to the invoice handler's credit-sale customer block.</summary>
internal sealed class StubCustomersModule(CustomerInfo? customer = null) : ICustomersModule
{
    public Task<CustomerInfo?> GetCustomerInfoAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(customer);

    public Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> DecreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RecentPaymentInfo>> GetRecentPaymentsAsync(
        int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerDebtRow>> GetDebtorsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerPaymentRow>> GetPaymentsAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>Serves a fixed set of expense rows (none by default) to the sales-period PDF.</summary>
internal sealed class StubExpensesModule(params ExpenseReportRow[] rows) : IExpensesModule
{
    public Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.Where(r => r.Date == date).Sum(r => r.Amount));

    public Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseReportRow> window = rows
            .Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to))
            .ToList();
        return Task.FromResult(window);
    }
}

/// <summary>Serves one fixed store profile to the invoice/label PDFs.</summary>
internal sealed class StubSettingsModule(StoreInfo store) : ISettingsModule
{
    public Task<string> GetStoreNameAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(store.StoreName);

    public Task<StoreInfo> GetStoreInfoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(store);
}
