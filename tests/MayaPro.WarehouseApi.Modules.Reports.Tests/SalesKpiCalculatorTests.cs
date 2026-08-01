using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetSalesKpi;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>
/// Unit tests for the pure <see cref="SalesKpiCalculator"/> (BE#27, SK-U1..SK-U4) — all figures from
/// in-memory inputs, no database.
/// </summary>
public sealed class SalesKpiCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);
    private const string Cash = WireFormat.PaymentTypes.Cash;
    private const string Card = WireFormat.PaymentTypes.Card;
    private const string Credit = WireFormat.PaymentTypes.Credit;

    private static SalesReportRow Sale(decimal total, decimal? profit, string paymentType) =>
        new(Today, total, profit, paymentType, ProductId: null, "P", Quantity: 1, UnitPrice: total, IsManual: false);

    [Fact]
    public void SK_U1_Happy_Path_Aggregates_And_Splits_By_Payment_Type()
    {
        var sales = new List<SalesReportRow>
        {
            Sale(100m, 40m, Cash),
            Sale(50m, 20m, Card),
            Sale(30m, 10m, Credit),
        };

        SalesKpiDto dto = SalesKpiCalculator.Build(sales);

        Assert.Equal(3, dto.SalesCount);
        Assert.Equal(180m, dto.TotalRevenue);
        Assert.Equal(70m, dto.TotalProfit);
        Assert.Equal(60m, dto.AvgSale);
        Assert.Equal(3, dto.ByPayment.Count);

        PaymentTypeKpiDto cash = dto.ByPayment.Single(p => p.Type == Cash);
        Assert.Equal(100m, cash.Revenue);
        Assert.Equal(40m, cash.Profit);

        PaymentTypeKpiDto card = dto.ByPayment.Single(p => p.Type == Card);
        Assert.Equal(50m, card.Revenue);
        Assert.Equal(20m, card.Profit);

        PaymentTypeKpiDto credit = dto.ByPayment.Single(p => p.Type == Credit);
        Assert.Equal(30m, credit.Revenue);
        Assert.Equal(10m, credit.Profit);
    }

    [Fact]
    public void SK_U2_Unknown_Profit_Is_Excluded_From_Total_But_Counted_And_Reported()
    {
        var sales = new List<SalesReportRow> { Sale(77m, null, Cash) };

        SalesKpiDto dto = SalesKpiCalculator.Build(sales);

        Assert.Equal(0m, dto.TotalProfit);            // unknown-profit row excluded, NOT counted as 0
        Assert.Equal(1, dto.UnknownProfitSalesCount);
        Assert.Equal(77m, dto.UnknownProfitAmount);
        Assert.Equal(77m, dto.TotalRevenue);           // revenue still counts — the money is real
    }

    [Fact]
    public void SK_U3_Empty_Period_Is_All_Zeros_With_Three_Zero_Payment_Rows()
    {
        SalesKpiDto dto = SalesKpiCalculator.Build([]);

        Assert.Equal(0, dto.SalesCount);
        Assert.Equal(0m, dto.TotalRevenue);
        Assert.Equal(0m, dto.TotalProfit);
        Assert.Equal(0m, dto.AvgSale);                  // no 0/0 throw
        Assert.Equal(3, dto.ByPayment.Count);
        Assert.All(dto.ByPayment, p => Assert.Equal(0m, p.Revenue));
        Assert.All(dto.ByPayment, p => Assert.Equal(0m, p.Profit));
    }

    [Fact]
    public void SK_U4_Untouched_Payment_Types_Still_Appear_At_Zero()
    {
        var sales = new List<SalesReportRow> { Sale(40m, 15m, Card), Sale(60m, 25m, Card) };

        SalesKpiDto dto = SalesKpiCalculator.Build(sales);

        Assert.Equal(0m, dto.ByPayment.Single(p => p.Type == Cash).Revenue);
        Assert.Equal(0m, dto.ByPayment.Single(p => p.Type == Credit).Revenue);
        Assert.Equal(100m, dto.ByPayment.Single(p => p.Type == Card).Revenue);
    }
}
