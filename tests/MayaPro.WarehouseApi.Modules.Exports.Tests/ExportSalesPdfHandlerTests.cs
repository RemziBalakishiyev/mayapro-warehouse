using System.Text;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSalesPdf;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// BE#19: <see cref="ExportSalesPdfHandler"/> must compute Cash/Card/Credit with the same "real money
/// received" formula as the Reports summary (<see cref="SalesReportRowTotals"/>) rather than the pre-BE#15
/// "TotalAmount of sales whose PaymentType is X" one — a smoke test, since the solution has no PDF
/// text-extraction dependency (same convention as <see cref="ExportSaleInvoicePdfHandlerTests"/>).
/// </summary>
public sealed class ExportSalesPdfHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 30);
    private static readonly StoreInfo Store = new("Test Mağaza", "Bakı", "0501112233", "AZN");

    [Fact]
    public async Task TC_Sales_Pdf_Export_Smoke_Test_For_A_Mixed_Day()
    {
        // Same TC12/BE#19 repro: Nağd 200 + Kart 150 + Nisyə(500/300 nağd alınıb) + Nisyə(100/0 alınıb).
        var sales = new StubSalesModuleForReport(
            Row(total: 200m, WireFormat.PaymentTypes.Cash, paidAmount: 200m, paidVia: WireFormat.PaymentTypes.Cash),
            Row(total: 150m, WireFormat.PaymentTypes.Card, paidAmount: 150m, paidVia: WireFormat.PaymentTypes.Card),
            Row(total: 500m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Cash),
            Row(total: 100m, WireFormat.PaymentTypes.Credit, paidAmount: 0m, paidVia: null));
        var handler = new ExportSalesPdfHandler(
            sales, new StubExpensesModuleForReport(), new StubSettingsModule(Store), new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler.Handle(Today, Today, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal($"satislar-{Today:yyyy-MM-dd}-{Today:yyyy-MM-dd}.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task An_Invalid_Range_Fails_Without_Touching_The_Sales_Module()
    {
        var handler = new ExportSalesPdfHandler(
            new StubSalesModuleForReport(), new StubExpensesModuleForReport(), new StubSettingsModule(Store),
            new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler.Handle(Today, Today.AddDays(-1), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Exports.InvalidRange", result.Error.Code);
    }

    private static SalesReportRow Row(decimal total, string paymentType, decimal? paidAmount, string? paidVia) =>
        new(Today, total, Profit: total / 2, paymentType, ProductId: null, "Test malı", Quantity: 1,
            UnitPrice: total, IsManual: false, PaidAmount: paidAmount, PaidVia: paidVia);

    private static void AssertIsPdf(ExportFileResult file)
    {
        Assert.NotEmpty(file.Content);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    /// <summary>Serves a fixed set of rows to the sales-period PDF/summary handlers.</summary>
    private sealed class StubSalesModuleForReport(params SalesReportRow[] rows) : ISalesModule
    {
        public Task<IReadOnlyList<SalesReportRow>> GetSalesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesReportRow>>(rows);

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

        public Task<Guid?> GetSaleIdByInvoiceTokenAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>An empty expenses feed — this handler's own arithmetic is out of BE#19's scope.</summary>
    private sealed class StubExpensesModuleForReport : IExpensesModule
    {
        public Task<decimal> GetDayTotalAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(0m);

        public Task<IReadOnlyList<ExpenseReportRow>> GetExpensesAsync(
            DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExpenseReportRow>>([]);
    }
}
