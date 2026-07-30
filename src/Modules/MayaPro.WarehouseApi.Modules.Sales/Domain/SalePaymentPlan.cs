namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// Resolves what actually gets stored for a sale's payment given what the caller requested (BE#15 — qismən
/// ödənişli satış). The single source of this rule: <c>CreateSale</c>/<c>UpdateSale</c>'s validator and
/// handler resolve through it, and so does <see cref="Sale"/> itself when a factory call omits the fields.
/// <list type="bullet">
///   <item><see cref="PaidAmount"/> defaults to the full total for a Cash/Card request, to zero for a Nisyə
///     request (back-compat, <see cref="PaidAmount"/> not supplied on the wire) — see <see cref="Resolve(PaymentType, decimal, decimal?, PaymentType?)"/>.</item>
///   <item><see cref="Remaining"/> is Total − PaidAmount. When positive, <see cref="PaymentType"/> is forced to
///     Credit regardless of what was requested — a sale with money still owed is a credit sale by definition —
///     and only this remaining amount (never the full total) is what raises the customer's debt.</item>
///   <item><see cref="PaidVia"/> records how the paid portion was actually received (Nağd/Kart). It is
///     meaningful even on a Nisyə sale that carries a cash/card down-payment; it defaults to Cash and, once
///     there is no remaining balance, always matches the stored <see cref="PaymentType"/> — a fully paid sale's
///     money can never be attributed to a different method than the one it is booked under.</item>
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
        string? paidViaCode) =>
        Resolve(
            requestedType,
            total,
            paidAmount,
            PaymentTypeCode.TryParse(paidViaCode, out PaymentType via) ? via : null);

    /// <param name="requestedType">The payment type asked for (already parsed).</param>
    /// <param name="total">The sale's total amount (unit price × quantity).</param>
    /// <param name="paidAmount">How much was received, or null to fall back to the AC2 default.</param>
    /// <param name="paidVia">How it was received, or null for the default (Cash, or the requested method).</param>
    public static SalePaymentPlan Resolve(
        PaymentType requestedType,
        decimal total,
        decimal? paidAmount,
        PaymentType? paidVia)
    {
        decimal effectivePaid = paidAmount ?? (requestedType == PaymentType.Credit ? 0m : total);
        decimal remaining = total - effectivePaid;

        // Money can only physically arrive as cash or card; "Nisyə" is an owing, not a method, so it (like an
        // unrecognised code) is discarded and the Cash default applies.
        PaymentType? receivedVia = paidVia is { } via && via != PaymentType.Credit ? via : null;

        PaymentType storedType = remaining > 0
            ? PaymentType.Credit
            // Nothing is owed → not a credit sale: a Nağd/Kart request keeps its own type, and a Nisyə request
            // that was settled in full at sale time is booked under the method the money actually arrived by.
            : requestedType == PaymentType.Credit ? receivedVia ?? PaymentType.Cash : requestedType;

        // Only a remaining balance lets the received method differ from the stored payment type.
        PaymentType resolvedVia = remaining > 0 ? receivedVia ?? PaymentType.Cash : storedType;

        return new SalePaymentPlan(effectivePaid, remaining, storedType, resolvedVia);
    }
}
