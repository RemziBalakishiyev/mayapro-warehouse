using MayaPro.WarehouseApi.Modules.Customers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetOpenDebts;

/// <summary>
/// BE#21 — every customer's still-unpaid debt sources, oldest first, computed at request time (nothing is
/// stored). A source is an opening balance or a sale that left a remaining balance; each customer's payments
/// are then written off against their sources FIFO (oldest source first), and only sources with something
/// left appear in the result.
/// <para>
/// FIFO is applied with the customer's payment TOTAL rather than payment by payment: since a payment can
/// never exceed the debt outstanding at the time it is taken (<see cref="Customer.DecreaseDebt"/>), writing
/// the payments off one by one — each starting at the oldest unpaid source — lands on exactly the same
/// allocation as writing off their sum. That lets the payments come back as one grouped query.
/// </para>
/// <para>
/// The whole list is four queries regardless of how many customers there are — customers, opening balances,
/// grouped payment totals and (via the Sales contract) outstanding sales — never a query per customer.
/// </para>
/// </summary>
public sealed class GetOpenDebtsHandler(
    ICustomersDbContext db,
    ISalesModule sales,
    IDateProvider dateProvider,
    ILogger<GetOpenDebtsHandler> logger)
{
    /// <summary>The description shown for an opening-balance row (the entity's own note is longer).</summary>
    public const string InitialDebtDescription = "İlkin borc";

    public async Task<OpenDebtsDto> Handle(CancellationToken ct)
    {
        List<Customer> customers = await db.Customers
            .AsNoTracking()
            .ToListAsync(ct);

        List<CustomerDebtAdjustment> adjustments = await db.CustomerDebtAdjustments
            .AsNoTracking()
            .ToListAsync(ct);

        // One grouped query for every customer's lifetime payment total — the pot FIFO spends.
        Dictionary<Guid, decimal> paidTotals = await db.CustomerPayments
            .AsNoTracking()
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Paid, ct);

        // One cross-module query for the sales that still owe money, across all customers.
        IReadOnlyList<CustomerOutstandingSale> outstandingSales = await sales.GetOutstandingSalesAsync(ct);

        // Sources that belong to a customer who no longer exists are dropped: deleting a customer leaves
        // their sales behind (CustomerId is a plain id, no FK), and such a row has nobody to bill.
        Dictionary<Guid, List<DebtSource>> sourcesByCustomer = adjustments
            .Select(a => new DebtSource(
                a.Id, a.CustomerId, CustomerHistoryEntryType.InitialDebt, a.Date, InitialDebtDescription, a.Amount))
            .Concat(outstandingSales.Select(s => new DebtSource(
                s.SaleId,
                s.CustomerId,
                CustomerHistoryEntryType.Sale,
                s.Date,
                $"{s.ProductName} × {s.Quantity}",
                s.RemainingAmount)))
            .GroupBy(s => s.CustomerId)
            // The Id tiebreak keeps FIFO allocation deterministic when two sources of the same customer
            // share the exact same instant (e.g. an opening balance and a same-day sale).
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Date).ThenBy(s => s.SourceId).ToList());

        DateOnly today = dateProvider.Today;
        var rows = new List<OpenDebtDto>();

        foreach (Customer customer in customers)
        {
            if (!sourcesByCustomer.TryGetValue(customer.Id, out List<DebtSource>? sources))
            {
                WarnIfDebtDoesNotMatch(customer, 0m);
                continue;
            }

            decimal unallocated = paidTotals.GetValueOrDefault(customer.Id);
            decimal customerRemaining = 0m;

            foreach (DebtSource source in sources)
            {
                decimal paidSoFar = Math.Min(unallocated, source.Amount);
                unallocated -= paidSoFar;
                decimal remaining = source.Amount - paidSoFar;

                // Fully settled sources are history, not open debt.
                if (remaining <= 0m)
                    continue;

                customerRemaining += remaining;
                rows.Add(new OpenDebtDto(
                    customer.Id,
                    customer.Name,
                    customer.Phone,
                    source.Source,
                    source.Date,
                    source.Description,
                    source.Amount,
                    paidSoFar,
                    remaining,
                    DaysOld(source.Date, today)));
            }

            WarnIfDebtDoesNotMatch(customer, customerRemaining);
        }

        // Oldest debt first. Sorting by the instant (rather than by the whole-day DaysOld) keeps rows from
        // the same day in a stable order; the customer name breaks ties between simultaneous sources, and
        // the customer id makes that final tiebreak deterministic even when two customers share a name.
        List<OpenDebtDto> ordered = rows
            .OrderBy(r => r.SourceDate)
            .ThenBy(r => r.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.CustomerId)
            .ToList();

        return new OpenDebtsDto(ordered, ordered.Sum(r => r.Remaining));
    }

    /// <summary>
    /// Business-zone (Asia/Baku) whole days between the source and today, floored at zero so a source
    /// stamped a few hours ahead of the local day never reports a negative age.
    /// </summary>
    private int DaysOld(DateTime sourceDateUtc, DateOnly today) =>
        Math.Max(0, today.DayNumber - dateProvider.ToLocalDate(sourceDateUtc).DayNumber);

    /// <summary>
    /// The rows' remaining sum must reconstruct the customer's stored <see cref="Customer.Debt"/>. A mismatch
    /// means the debt balance and its sources have drifted apart (e.g. an old row written before this rule):
    /// it is a data-quality signal, not a request failure, so it is logged and the list is still served.
    /// </summary>
    private void WarnIfDebtDoesNotMatch(Customer customer, decimal remainingSum)
    {
        if (remainingSum == customer.Debt)
            return;

        logger.LogWarning(
            "Open debts mismatch for customer {CustomerId}: sources remain {RemainingSum} but stored debt is {Debt}",
            customer.Id,
            remainingSum,
            customer.Debt);
    }

    /// <summary>
    /// One debt source before FIFO allocation: what raised the debt, when, and by how much.
    /// <see cref="SourceId"/> (the adjustment's or sale's own id) is only a deterministic tiebreak for
    /// sources that share the exact same <see cref="Date"/> — it never reaches the response.
    /// </summary>
    private sealed record DebtSource(
        Guid SourceId,
        Guid CustomerId,
        string Source,
        DateTime Date,
        string Description,
        decimal Amount);
}
