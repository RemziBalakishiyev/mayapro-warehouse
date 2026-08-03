using MayaPro.WarehouseApi.Modules.DayEnd.Domain;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Tests;

/// <summary>
/// Domain unit tests for <see cref="Closing"/> — the expected-cash and difference maths:
/// ExpectedCash = OpeningCash + CashSales − Expenses; Difference = ActualCash − ExpectedCash.
/// </summary>
public sealed class ClosingTests
{
    private static Closing Create(
        decimal openingCash, decimal cashSales, decimal expenses, decimal actualCash, decimal salaryExpenses = 0m) =>
        Closing.Create(
            date: new DateOnly(2026, 7, 11),
            openingCash: openingCash,
            cashSales: cashSales,
            cardSales: 0,
            nisyeSales: 0,
            expenses: expenses,
            salaryExpenses: salaryExpenses,
            actualCash: actualCash,
            closedByUserId: null,
            note: null);

    [Fact]
    public void ExpectedCash_Is_Opening_Plus_Cash_Minus_Expenses()
    {
        Closing closing = Create(openingCash: 100, cashSales: 200, expenses: 50, actualCash: 250);

        Assert.Equal(250m, closing.ExpectedCash); // 100 + 200 − 50
    }

    [Fact]
    public void Surplus_Gives_Positive_Difference()
    {
        Closing closing = Create(openingCash: 100, cashSales: 200, expenses: 50, actualCash: 260);

        Assert.Equal(10m, closing.Difference); // 260 − 250
    }

    [Fact]
    public void Shortfall_Gives_Negative_Difference()
    {
        Closing closing = Create(openingCash: 100, cashSales: 200, expenses: 50, actualCash: 240);

        Assert.Equal(-10m, closing.Difference); // 240 − 250
    }

    [Fact]
    public void Exact_Match_Gives_Zero_Difference()
    {
        Closing closing = Create(openingCash: 100, cashSales: 200, expenses: 50, actualCash: 250);

        Assert.Equal(0m, closing.Difference);
    }

    /// <summary>
    /// BE#33: <c>SalaryExpenses</c> is stored as an informational breakdown of <c>Expenses</c> — it must
    /// never change <c>ExpectedCash</c>/<c>Difference</c>, which already folded salary payments into
    /// <c>Expenses</c> before this field existed (BE#28).
    /// </summary>
    [Fact]
    public void SalaryExpenses_Is_Stored_Without_Changing_ExpectedCash_Or_Difference()
    {
        Closing closing = Create(
            openingCash: 100, cashSales: 200, expenses: 240, actualCash: 90, salaryExpenses: 200);

        Assert.Equal(200m, closing.SalaryExpenses);
        Assert.Equal(60m, closing.ExpectedCash);   // 100 + 200 − 240 (240 already includes the 200 salary)
        Assert.Equal(30m, closing.Difference);     // 90 − 60
    }

    /// <summary>Default (no salary payments) keeps SalaryExpenses at zero — the pre-BE#33 shape.</summary>
    [Fact]
    public void SalaryExpenses_Defaults_To_Zero()
    {
        Closing closing = Create(openingCash: 100, cashSales: 200, expenses: 50, actualCash: 250);

        Assert.Equal(0m, closing.SalaryExpenses);
    }
}
