using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// BE#19: <see cref="SalesReportRowTotals.ComputeReceivedTotals"/> is the single formula behind both the
/// Reports summary and the sales PDF export — Cash/Card are the real amount received via each method
/// (including a Nisyə sale's down-payment), Credit is only what remains unpaid.
/// </summary>
public sealed class SalesReportRowTotalsTests
{
    private static readonly DateOnly Today = new(2026, 7, 30);

    [Fact]
    public void TC_Mixed_Day_Splits_Cash_Card_And_Credit_Correctly()
    {
        // Nağd 200 (fully paid) + Kart 150 (fully paid) + Nisyə(500 total, 300 nağd alınıb) +
        // Nisyə(100 total, 0 alınıb) → Cash=500 (200 + 300 down-payment), Card=150, Credit=300 (200 + 100).
        SalesReportRow[] rows =
        [
            Row(total: 200m, WireFormat.PaymentTypes.Cash, paidAmount: 200m, paidVia: WireFormat.PaymentTypes.Cash),
            Row(total: 150m, WireFormat.PaymentTypes.Card, paidAmount: 150m, paidVia: WireFormat.PaymentTypes.Card),
            Row(total: 500m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Cash),
            Row(total: 100m, WireFormat.PaymentTypes.Credit, paidAmount: 0m, paidVia: null),
        ];

        SalesDayTotals totals = rows.ComputeReceivedTotals();

        Assert.Equal(500m, totals.Cash);
        Assert.Equal(150m, totals.Card);
        Assert.Equal(300m, totals.Credit);
    }

    [Fact]
    public void No_Rows_Yields_All_Zeros()
    {
        SalesDayTotals totals = Array.Empty<SalesReportRow>().ComputeReceivedTotals();

        Assert.Equal(0m, totals.Cash);
        Assert.Equal(0m, totals.Card);
        Assert.Equal(0m, totals.Credit);
    }

    [Fact]
    public void Fully_Paid_Credit_Row_Contributes_Nothing_To_Credit()
    {
        SalesReportRow[] rows = [Row(total: 300m, WireFormat.PaymentTypes.Credit, paidAmount: 300m, paidVia: WireFormat.PaymentTypes.Card)];

        SalesDayTotals totals = rows.ComputeReceivedTotals();

        Assert.Equal(0m, totals.Cash);
        Assert.Equal(300m, totals.Card); // the paid-in-full portion is still real card income
        Assert.Equal(0m, totals.Credit);
    }

    [Fact]
    public void Legacy_Rows_Without_PaidAmount_Fall_Back_To_The_Sales_TotalAmount_Rule()
    {
        // Rows constructed without PaidAmount/PaidVia (pre-BE#15 shape) still default sensibly:
        // nothing received on a Nisyə row, the full total on a Nağd/Kart one.
        SalesReportRow[] rows =
        [
            new(Today, 100m, 40m, WireFormat.PaymentTypes.Cash, null, "P", 1, 100m, IsManual: false),
            new(Today, 250m, 90m, WireFormat.PaymentTypes.Credit, null, "P", 1, 250m, IsManual: false),
        ];

        SalesDayTotals totals = rows.ComputeReceivedTotals();

        Assert.Equal(100m, totals.Cash);
        Assert.Equal(0m, totals.Card);
        Assert.Equal(250m, totals.Credit);
    }

    private static SalesReportRow Row(decimal total, string paymentType, decimal? paidAmount, string? paidVia) =>
        new(Today, total, Profit: total / 2, paymentType, ProductId: null, "Test malı", Quantity: 1,
            UnitPrice: total, IsManual: false, PaidAmount: paidAmount, PaidVia: paidVia);
}
