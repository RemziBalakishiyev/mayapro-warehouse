using System.Text;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSalesPdf;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// BE#19: <see cref="ExportSalesPdfHandler"/> must compute Cash/Card/Credit with the same "real money
/// received" formula as the Reports summary (<see cref="SalesReportRowTotals"/>) rather than the pre-BE#15
/// "TotalAmount of sales whose PaymentType is X" one. The figures themselves are asserted where they are
/// computed (<c>SalesReportRowTotalsTests</c>, <c>GetSummaryHandlerTests</c>); here the handler is covered at
/// the magic-bytes/byte-diff level — the same convention as <see cref="ExportSaleInvoicePdfHandlerTests"/>,
/// since the solution carries no PDF text-extraction dependency.
/// </summary>
public sealed class ExportSalesPdfHandlerTests
{
    private static readonly DateOnly Today = new(2026, 7, 30);
    private static readonly StoreInfo Store = new("Test Mağaza", "Bakı", "0501112233", "AZN");

    [Fact]
    public async Task TC_Sales_Pdf_Export_Smoke_Test_For_A_Mixed_Day()
    {
        // Same TC4/BE#19 repro: Nağd 200 + Kart 150 + Nisyə(500/300 nağd alınıb) + Nisyə(100/0 alınıb).
        Result<ExportFileResult> result = await Handler(MixedDay()).Handle(Today, Today, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.Equal($"satislar-{Today:yyyy-MM-dd}-{Today:yyyy-MM-dd}.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task The_Report_Is_Built_From_The_Requested_Window_Only()
    {
        // Yesterday's sale must not reach a "today only" report — proven by that report coming out smaller
        // than the one whose window also covers yesterday (one row fewer in the table).
        SalesReportRow[] rows =
        [
            Row(Today, total: 200m, WireFormat.PaymentTypes.Cash, paidAmount: 200m, paidVia: WireFormat.PaymentTypes.Cash),
            Row(Today.AddDays(-1), total: 900m, WireFormat.PaymentTypes.Cash, paidAmount: 900m, paidVia: WireFormat.PaymentTypes.Cash),
        ];

        Result<ExportFileResult> todayOnly = await Handler(rows).Handle(Today, Today, default);
        Result<ExportFileResult> bothDays = await Handler(rows).Handle(Today.AddDays(-1), Today, default);

        Assert.True(todayOnly.IsSuccess);
        Assert.True(bothDays.IsSuccess);
        AssertIsPdf(todayOnly.Value);
        Assert.True(
            todayOnly.Value.Content.Length < bothDays.Value.Content.Length,
            "the narrower window should print one sale row fewer");
    }

    [Fact]
    public async Task A_Partially_Paid_Credit_Row_Prints_Its_Odenildi_Qaliq_Split()
    {
        // BE#19: only a Nisyə row that carries a down-payment gets the extra "Ödənildi / Qalıq" lines, so an
        // otherwise identical fully unpaid row must produce a strictly smaller document.
        SalesReportRow[] partiallyPaid =
            [Row(Today, total: 500m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Cash)];
        SalesReportRow[] unpaid =
            [Row(Today, total: 500m, WireFormat.PaymentTypes.Credit, paidAmount: 0m, paidVia: null)];

        Result<ExportFileResult> partial = await Handler(partiallyPaid).Handle(Today, Today, default);
        Result<ExportFileResult> none = await Handler(unpaid).Handle(Today, Today, default);

        Assert.True(partial.IsSuccess);
        Assert.True(none.IsSuccess);
        AssertIsPdf(partial.Value);
        AssertIsPdf(none.Value);
        Assert.True(
            partial.Value.Content.Length > none.Value.Content.Length,
            "the partially paid row should carry the extra Ödənildi/Qalıq lines");
    }

    [Fact]
    public async Task An_Empty_Period_Still_Produces_A_Valid_Pdf()
    {
        Result<ExportFileResult> result = await Handler([]).Handle(Today, Today, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
    }

    [Fact]
    public async Task An_Invalid_Range_Fails_Without_Touching_The_Sales_Module()
    {
        // The stub's report half is deliberately left unset: reaching it would throw instead of failing cleanly.
        var handler = new ExportSalesPdfHandler(
            new StubSalesModule(), new StubExpensesModule(), new StubSettingsModule(Store),
            new FixedDateProvider(Today));

        Result<ExportFileResult> result = await handler.Handle(Today, Today.AddDays(-1), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("Exports.InvalidRange", result.Error.Code);
    }

    private static SalesReportRow[] MixedDay() =>
    [
        Row(Today, total: 200m, WireFormat.PaymentTypes.Cash, paidAmount: 200m, paidVia: WireFormat.PaymentTypes.Cash),
        Row(Today, total: 150m, WireFormat.PaymentTypes.Card, paidAmount: 150m, paidVia: WireFormat.PaymentTypes.Card),
        Row(Today, total: 500m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Cash),
        Row(Today, total: 100m, WireFormat.PaymentTypes.Credit, paidAmount: 0m, paidVia: null),
    ];

    private static ExportSalesPdfHandler Handler(params SalesReportRow[] rows) =>
        new(
            StubSalesModule.ForReport(rows),
            new StubExpensesModule(),
            new StubSettingsModule(Store),
            new FixedDateProvider(Today));

    private static SalesReportRow Row(
        DateOnly date, decimal total, string paymentType, decimal? paidAmount, string? paidVia) =>
        new(date, total, Profit: total / 2, paymentType, ProductId: null, "Test malı", Quantity: 1,
            UnitPrice: total, IsManual: false, PaidAmount: paidAmount, PaidVia: paidVia);

    private static void AssertIsPdf(ExportFileResult file)
    {
        Assert.NotEmpty(file.Content);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }
}
