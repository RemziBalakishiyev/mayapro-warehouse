namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// Resolves what actually gets stored for a sale's payment given what the caller requested (BE#15 — qismən
/// ödənişli satış). Shared by <c>CreateSale</c>/<c>UpdateSale</c>'s validator and handler so this business rule
/// lives in exactly one place:
/// <list type="bullet">
///   <item><see cref="PaidAmount"/> defaults to the full total for a Cash/Card request, to zero for a Nisyə
///     request (back-compat, <see cref="PaidAmount"/> not supplied on the wire) — see <see cref="Resolve"/>.</item>
///   <item><see cref="Remaining"/> is Total − PaidAmount. When positive, <see cref="PaymentType"/> is forced to
///     Credit regardless of what was requested — a sale with money still owed is a credit sale by definition —
///     and only this remaining amount (never the full total) is what raises the customer's debt.</item>
///   <item><see cref="PaidVia"/> records how the paid portion was actually received (Nağd/Kart). It is
///     meaningful even on a Nisyə sale that carries a cash/card down-payment; it defaults to Cash and, once
///     there is no remaining balance, always matches the stored <see cref="PaymentType"/>.</item>
/// </list>
/// </summary>
public readonly record struct SalePaymentPlan(
    decimal PaidAmount,
    decimal Remaining,
    PaymentType PaymentType,
    PaymentType PaidVia)
{
    /// <param name="requestedType">The payment type asked for on the wire (already parsed).</param>
    /// <param name="total">The sale's total amount (unit price × quantity).</param>
    /// <param name="paidAmount">The wire <c>paidAmount</c>, or null to fall back to the AC2 default.</param>
    /// <param name="paidViaCode">The wire <c>paidVia</c> code (<c>"Nağd"|"Kart"</c>), or null for the default.</param>
    public static SalePaymentPlan Resolve(
        PaymentType requestedType,
        decimal total,
        decimal? paidAmount,
        string? paidViaCode)
    {
        decimal effectivePaid = paidAmount ?? (requestedType == PaymentType.Credit ? 0m : total);
        decimal remaining = total - effectivePaid;

        PaymentType storedType = remaining > 0
            ? PaymentType.Credit
            : requestedType == PaymentType.Credit ? PaymentType.Cash : requestedType;

        PaymentType paidVia;
        if (remaining > 0)
        {
            // How the (partial) payment was received; only Cash/Card are valid — never "Credit" itself.
            bool parsed = PaymentTypeCode.TryParse(paidViaCode, out PaymentType via);
            paidVia = parsed && via != PaymentType.Credit ? via : PaymentType.Cash;
        }
        else
        {
            paidVia = storedType;
        }

        return new SalePaymentPlan(effectivePaid, remaining, storedType, paidVia);
    }
}
