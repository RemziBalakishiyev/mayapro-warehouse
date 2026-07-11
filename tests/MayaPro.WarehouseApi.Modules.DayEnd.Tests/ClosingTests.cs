using MayaPro.WarehouseApi.Modules.DayEnd.Domain;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Tests;

/// <summary>
/// Domain unit tests for <see cref="Closing"/> — the expected-cash and difference maths:
/// ExpectedCash = OpeningCash + CashSales − Expenses; Difference = ActualCash − ExpectedCash.
/// </summary>
public sealed class ClosingTests
{
    private static Closing Create(decimal openingCash, decimal cashSales, decimal expenses, decimal actualCash) =>
        Closing.Create(
            date: new DateOnly(2026, 7, 11),
            openingCash: openingCash,
            cashSales: cashSales,
            cardSales: 0,
            nisyeSales: 0,
            expenses: expenses,
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
}
