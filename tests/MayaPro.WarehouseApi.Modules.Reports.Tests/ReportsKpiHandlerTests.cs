using MayaPro.WarehouseApi.Modules.Reports.Application;
using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDebtsKpi;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetProductsKpi;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSalesKpi;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>
/// Unit tests for the three BE#27 KPI handlers' own responsibilities — date-range validation (from &gt; to
/// → 400) and, for products/debts, the small window-resolution glue around the pure calculators. The
/// calculators' maths is covered separately by <c>ProductsKpiCalculatorTests</c>/<c>SalesKpiCalculatorTests</c>/
/// <c>DebtsKpiCalculatorTests</c>.
/// </summary>
public sealed class ReportsKpiHandlerTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);

    [Fact]
    public async Task ProductsKpi_Reversed_Range_Is_Rejected()
    {
        var handler = new GetProductsKpiHandler(
            new KpiFakeProductsModule(), new KpiFakeSalesModule(), new KpiFixedDateProvider(Today));

        Result<ProductsKpiDto> result = await handler.Handle(Today, Today.AddDays(-1), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ReportErrors.InvalidDateRange.Code, result.Error.Code);
    }

    [Fact]
    public async Task ProductsKpi_Empty_Range_Is_Unbounded_And_Succeeds()
    {
        var handler = new GetProductsKpiHandler(
            new KpiFakeProductsModule(), new KpiFakeSalesModule(), new KpiFixedDateProvider(Today));

        Result<ProductsKpiDto> result = await handler.Handle(null, null, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProductsKpi_Counts_A_New_Products_Opening_Stock_Only_Within_The_Window()
    {
        Guid inWindow = Guid.NewGuid();
        Guid beforeWindow = Guid.NewGuid();
        var snapshots = new List<ProductSnapshot>
        {
            new(inWindow, "New", "Cat", 20, 1, 5m, 8m, InitialQuantity: 20,
                CreatedAt: Today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            new(beforeWindow, "Old", "Cat", 5, 1, 5m, 8m, InitialQuantity: 5,
                CreatedAt: Today.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        };
        var handler = new GetProductsKpiHandler(
            new KpiFakeProductsModule(snapshots), new KpiFakeSalesModule(), new KpiFixedDateProvider(Today));

        Result<ProductsKpiDto> result = await handler.Handle(Today, Today, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value.PurchasedUnits); // only the product created inside [Today, Today]
    }

    [Fact]
    public async Task SalesKpi_Reversed_Range_Is_Rejected()
    {
        var handler = new GetSalesKpiHandler(new KpiFakeSalesModule());

        Result<SalesKpiDto> result = await handler.Handle(Today, Today.AddDays(-1), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ReportErrors.InvalidDateRange.Code, result.Error.Code);
    }

    [Fact]
    public async Task SalesKpi_Empty_Range_Is_Unbounded_And_Succeeds()
    {
        var handler = new GetSalesKpiHandler(new KpiFakeSalesModule());

        Result<SalesKpiDto> result = await handler.Handle(null, null, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DebtsKpi_Reversed_Range_Is_Rejected()
    {
        var handler = new GetDebtsKpiHandler(
            new KpiFakeCustomersModule(), new KpiFakeSalesModule(), new KpiFixedDateProvider(Today));

        Result<DebtsKpiDto> result = await handler.Handle(Today, Today.AddDays(-1), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ReportErrors.InvalidDateRange.Code, result.Error.Code);
    }

    [Fact]
    public async Task DebtsKpi_Resolves_OldestDebtDate_From_The_Oldest_Outstanding_Sale()
    {
        var outstanding = new List<CustomerOutstandingSale>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Today.AddDays(-12).ToDateTime(TimeOnly.MinValue), "P", 1, 40m),
        };
        var handler = new GetDebtsKpiHandler(
            new KpiFakeCustomersModule(), new KpiFakeSalesModule(outstanding), new KpiFixedDateProvider(Today));

        Result<DebtsKpiDto> result = await handler.Handle(null, null, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.OldestDebtDays);
    }

    [Fact]
    public async Task DebtsKpi_No_Outstanding_Sales_Means_Null_OldestDebtDays()
    {
        var handler = new GetDebtsKpiHandler(
            new KpiFakeCustomersModule(), new KpiFakeSalesModule(), new KpiFixedDateProvider(Today));

        Result<DebtsKpiDto> result = await handler.Handle(null, null, default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.OldestDebtDays);
    }

    private sealed class KpiFixedDateProvider(DateOnly today) : IDateProvider
    {
        public DateTime UtcNow => today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        public DateOnly Today => today;

        public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

        public DateTime ToLocalDateTime(DateTime utc) => utc;

        public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
            (localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private sealed class KpiFakeProductsModule(IReadOnlyList<ProductSnapshot>? snapshots = null) : IProductsModule
    {
        public Task<IReadOnlyList<ProductSnapshot>> GetAllSnapshotsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshots ?? []);

        public Task<IReadOnlyList<StockAdjustmentRow>> GetStockAdjustmentsAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StockAdjustmentRow>>([]);

        public Task<Result<ProductStockSnapshot>> TryDecreaseStockAsync(
            Guid productId, int quantity, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> IncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ProductSnapshot>> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken = default) =>
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

        public Task<IReadOnlyList<ProductLabelInfo>> GetLabelInfoAsync(
            IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class KpiFakeSalesModule(IReadOnlyList<CustomerOutstandingSale>? outstanding = null) : ISalesModule
    {
        public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesReportRow>>([]);

        public Task<IReadOnlyList<CustomerOutstandingSale>> GetOutstandingSalesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(outstanding ?? []);

        public Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProductLastSale>> GetLastSaleDatesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecentSaleInfo>> GetRecentSalesAsync(int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerPurchaseStats>> GetPurchaseStatsByCustomerAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerSaleEntry>> GetSalesByCustomerAsync(
            Guid customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> DeleteCreditSaleAsync(
            Guid saleId, Guid customerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SaleInvoiceInfo?> GetInvoiceSaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<InvoiceTokenOwner?> GetInvoiceTokenOwnerAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class KpiFakeCustomersModule : ICustomersModule
    {
        public Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0m);

        public Task<IReadOnlyList<CustomerDebtRow>> GetDebtorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDebtRow>>([]);

        public Task<IReadOnlyList<CustomerPaymentRow>> GetPaymentsAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerPaymentRow>>([]);

        public Task<Result> IncreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> DecreaseDebtAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecentPaymentInfo>> GetRecentPaymentsAsync(
            int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Dictionary<Guid, string>> GetNamesAsync(
            IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CustomerInfo?> GetCustomerInfoAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
