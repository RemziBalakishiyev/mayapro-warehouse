using MayaPro.WarehouseApi.Modules.Reports.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Reports.Application.UseCases.GetDebtsKpi;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Reports.Tests;

/// <summary>
/// Unit tests for the pure <see cref="DebtsKpiCalculator"/> (BE#27, DK-U1..DK-U5) — all figures from
/// in-memory inputs, no database.
/// </summary>
public sealed class DebtsKpiCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);
    private const string Credit = WireFormat.PaymentTypes.Credit;

    private static CustomerDebtRow Debtor(string name, decimal debt) => new(Guid.NewGuid(), name, debt);

    private static SalesReportRow CreditSale(decimal total, decimal paidAmount) =>
        new(Today, total, Profit: 0m, Credit, ProductId: null, "P", Quantity: 1, UnitPrice: total,
            IsManual: false, PaidAmount: paidAmount, PaidVia: null);

    private static CustomerPaymentRow Payment(decimal amount) => new(Today, amount);

    [Fact]
    public void DK_U1_Happy_Path_Debtors_TopDebtor_And_OldestDebtDays()
    {
        var debtors = new List<CustomerDebtRow> { Debtor("Əli", 500m), Debtor("Vəli", 200m), Debtor("Səda", 50m) };

        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 750m,
            debtors: debtors,
            salesInPeriod: [],
            paymentsInPeriod: [],
            oldestDebtDate: Today.AddDays(-12),
            today: Today);

        Assert.Equal(750m, dto.TotalOutstanding);
        Assert.Equal(3, dto.DebtorCount);
        Assert.NotNull(dto.TopDebtor);
        Assert.Equal("Əli", dto.TopDebtor!.Name);
        Assert.Equal(500m, dto.TopDebtor.Amount);
        Assert.Equal(12, dto.OldestDebtDays);
    }

    [Fact]
    public void DK_U2_No_Debtors_Means_Null_TopDebtor_And_Null_OldestDebtDays()
    {
        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 0m,
            debtors: [],
            salesInPeriod: [],
            paymentsInPeriod: [],
            oldestDebtDate: null,
            today: Today);

        Assert.Equal(0, dto.DebtorCount);
        Assert.Null(dto.TopDebtor);
        Assert.Null(dto.OldestDebtDays);
        Assert.Equal(0m, dto.TotalOutstanding);
    }

    [Fact]
    public void DK_U3_PeriodNewDebt_Uses_TotalAmount_Minus_Received_For_Credit_Sales()
    {
        var sales = new List<SalesReportRow> { CreditSale(total: 100m, paidAmount: 30m) };

        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 0m, debtors: [], salesInPeriod: sales, paymentsInPeriod: [],
            oldestDebtDate: null, today: Today);

        Assert.Equal(70m, dto.PeriodNewDebt); // 100 - 30
    }

    [Fact]
    public void DK_U4_PeriodCollected_Sums_The_Periods_Payments()
    {
        var payments = new List<CustomerPaymentRow> { Payment(150m), Payment(80m) };

        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 0m, debtors: [], salesInPeriod: [], paymentsInPeriod: payments,
            oldestDebtDate: null, today: Today);

        Assert.Equal(230m, dto.PeriodCollected);
    }

    [Fact]
    public void DK_U5_Tied_Debt_Breaks_Deterministically_By_Name()
    {
        // Documented tie-break: highest debt first, equal amounts fall back to the name (alphabetical).
        var debtors = new List<CustomerDebtRow> { Debtor("Zaur", 300m), Debtor("Anar", 300m) };

        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 600m, debtors: debtors, salesInPeriod: [], paymentsInPeriod: [],
            oldestDebtDate: null, today: Today);

        Assert.NotNull(dto.TopDebtor);
        Assert.Equal("Anar", dto.TopDebtor!.Name); // alphabetically first among the tied amounts
        Assert.Equal(300m, dto.TopDebtor.Amount);
    }

    [Fact]
    public void PeriodNewDebt_Ignores_Cash_And_Card_Sales()
    {
        var sales = new List<SalesReportRow>
        {
            new(Today, 200m, 50m, WireFormat.PaymentTypes.Cash, null, "P", 1, 200m, false),
            CreditSale(total: 100m, paidAmount: 0m),
        };

        DebtsKpiDto dto = DebtsKpiCalculator.Build(
            totalOutstanding: 0m, debtors: [], salesInPeriod: sales, paymentsInPeriod: [],
            oldestDebtDate: null, today: Today);

        Assert.Equal(100m, dto.PeriodNewDebt); // only the credit sale raises new debt
    }
}
