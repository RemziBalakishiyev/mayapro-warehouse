using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Domain;

/// <summary>
/// A day-end cash reconciliation. The sales/expense totals are computed server-side; expected cash and
/// the difference are derived here so the maths is single-sourced:
/// <c>ExpectedCash = OpeningCash + CashSales − Expenses</c>, <c>Difference = ActualCash − ExpectedCash</c>.
/// </summary>
public sealed class Closing : Entity
{
    // EF Core constructor.
    private Closing() { }

    private Closing(
        DateOnly date,
        decimal openingCash,
        decimal cashSales,
        decimal cardSales,
        decimal nisyeSales,
        decimal expenses,
        decimal actualCash,
        Guid? closedByUserId,
        string? note)
    {
        Date = date;
        OpeningCash = openingCash;
        CashSales = cashSales;
        CardSales = cardSales;
        NisyeSales = nisyeSales;
        Expenses = expenses;
        ActualCash = actualCash;
        ClosedByUserId = closedByUserId;
        Note = note;
        ExpectedCash = openingCash + cashSales - expenses;
        Difference = actualCash - ExpectedCash;
    }

    public DateOnly Date { get; private set; }

    public decimal OpeningCash { get; private set; }

    public decimal CashSales { get; private set; }

    public decimal CardSales { get; private set; }

    /// <summary>Credit (Nisyə) sales for the day. Not part of the cash reconciliation.</summary>
    public decimal NisyeSales { get; private set; }

    public decimal Expenses { get; private set; }

    /// <summary>Expected cash in the drawer: opening + cash sales − expenses.</summary>
    public decimal ExpectedCash { get; private set; }

    /// <summary>Counted cash in the drawer.</summary>
    public decimal ActualCash { get; private set; }

    /// <summary>Actual − expected (negative = shortfall, positive = surplus).</summary>
    public decimal Difference { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public string? Note { get; private set; }

    public static Closing Create(
        DateOnly date,
        decimal openingCash,
        decimal cashSales,
        decimal cardSales,
        decimal nisyeSales,
        decimal expenses,
        decimal actualCash,
        Guid? closedByUserId,
        string? note) =>
        new(date, openingCash, cashSales, cardSales, nisyeSales, expenses, actualCash, closedByUserId, note);
}
