using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// A completed sale. A normal sale references a catalogued product and snapshots the product name,
/// category and its real cost at sale time, so historical profit (and category reporting) stay stable
/// even if the product later changes. A free-form ("manual") sale has no product: the seller types the
/// name by hand and may or may not know the cost — when the cost is unknown, <see cref="Profit"/> is
/// left null rather than inventing a phantom gain. Category on a manual sale is optional.
/// Amounts are computed once in <see cref="Create"/> / <see cref="CreateManual"/>.
/// </summary>
public sealed class Sale : TenantEntity
{
    // EF Core constructor.
    private Sale() { }

    private Sale(
        Guid? productId,
        bool isManual,
        string productName,
        string? category,
        int quantity,
        decimal unitPrice,
        decimal subtotal,
        decimal totalAmount,
        decimal? costPerUnit,
        decimal? purchasePricePerUnit,
        decimal? profit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        DateTime date,
        IReadOnlyList<SaleExpenseItem> expenseItems,
        decimal paidAmount,
        PaymentType paidVia)
    {
        ProductId = productId;
        IsManual = isManual;
        ProductName = productName;
        Category = category;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = subtotal;
        TotalAmount = totalAmount;
        CostPerUnit = costPerUnit;
        PurchasePricePerUnit = purchasePricePerUnit;
        Profit = profit;
        PaymentType = paymentType;
        CustomerId = customerId;
        SoldByUserId = soldByUserId;
        SoldByName = soldByName;
        Date = date;
        ExpenseItems = expenseItems;
        PaidAmount = paidAmount;
        PaidVia = paidVia;
    }

    /// <summary>
    /// Opaque token behind the public (auth-suz) invoice link. Created lazily on the first link request
    /// and never regenerated, so a shared WhatsApp link stays valid for the sale's lifetime.
    /// </summary>
    public string? InvoiceToken { get; private set; }

    /// <summary>Assigns the public-link token once; later calls keep the original (link stability).</summary>
    public void AssignInvoiceToken(string token) => InvoiceToken ??= token;

    /// <summary>The catalogued product sold; null for a free-form (manual) sale.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>True when the item was typed by hand and is not in the catalogue (no product, no stock move).</summary>
    public bool IsManual { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    /// <summary>
    /// Category snapshot at sale time. For a catalogued sale this is the product's category; for a manual
    /// sale it is whatever the seller supplied (or null). Existing rows predate this field and stay null.
    /// </summary>
    public string? Category { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    /// <summary><see cref="UnitPrice"/> × <see cref="Quantity"/>.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Sold total: same as <see cref="Subtotal"/> (<see cref="UnitPrice"/> is the sale price).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Real cost per unit at sale time (snapshot). Null on a manual sale whose cost is unknown.</summary>
    public decimal? CostPerUnit { get; private set; }

    /// <summary>
    /// Purchase price per unit at sale time — separate from <see cref="CostPerUnit"/> (which also folds in
    /// spread batch expenses). For a catalogued sale this is the product's <c>PurchasePrice</c> snapshotted
    /// at sale time; for a manual sale it is whatever the seller supplied (already known, not recomputed).
    /// Null when unknown (older rows predate this field, or a manual sale did not supply it).
    /// </summary>
    public decimal? PurchasePricePerUnit { get; private set; }

    /// <summary>
    /// (<see cref="UnitPrice"/> − <see cref="CostPerUnit"/>) × <see cref="Quantity"/>.
    /// Null when the cost is unknown (a manual sale with no cost) — the gain is genuinely unknown, so reports
    /// exclude it rather than counting it as zero profit.
    /// </summary>
    public decimal? Profit { get; private set; }

    public PaymentType PaymentType { get; private set; }

    /// <summary>
    /// How much was actually received at sale time (BE#15 — qismən ödənişli satış). Equals
    /// <see cref="TotalAmount"/> when the sale was paid in full; less than it — down to zero — when a Nisyə
    /// sale carries a partial (or no) down-payment. Never negative, never above <see cref="TotalAmount"/>
    /// (validator's rule).
    /// </summary>
    public decimal PaidAmount { get; private set; }

    /// <summary><see cref="TotalAmount"/> − <see cref="PaidAmount"/>. Zero on a fully paid sale.</summary>
    public decimal RemainingAmount => TotalAmount - PaidAmount;

    /// <summary>
    /// How the paid portion was received (Nağd/Kart) — meaningful even on a Nisyə sale with a cash/card
    /// down-payment, since that money is real cash-drawer (or card-settlement) income the same day. Never
    /// Credit: an owing is not a way of receiving money. On a sale stored as Nağd/Kart (i.e. nothing remains)
    /// this always equals <see cref="PaymentType"/>, so the same money is never counted under two methods.
    /// </summary>
    public PaymentType PaidVia { get; private set; }

    /// <summary>
    /// The buying customer, optional on every payment type (mandatory only when <see cref="RemainingAmount"/>
    /// is positive — validator's rule). Only a sale with a remaining balance touches the customer's debt, and
    /// only by that remaining amount; on a fully paid sale this is purchase-history bookkeeping.
    /// </summary>
    public Guid? CustomerId { get; private set; }

    public Guid? SoldByUserId { get; private set; }

    public string SoldByName { get; private set; } = string.Empty;

    public DateTime Date { get; private set; }

    /// <summary>
    /// Free-form expense lines that document how a manual sale's cost was worked out. Empty on a catalogued
    /// sale (its cost comes from the product) and on manual sales where the seller entered no breakdown.
    /// Stored inline as a JSON array; never used to (re)compute <see cref="Profit"/>.
    /// </summary>
    public IReadOnlyList<SaleExpenseItem> ExpenseItems { get; private set; } = Array.Empty<SaleExpenseItem>();

    public static Sale Create(
        Guid productId,
        string productName,
        string? category,
        int quantity,
        decimal unitPrice,
        decimal costPerUnit,
        decimal purchasePricePerUnit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        decimal? paidAmount = null,
        PaymentType? paidVia = null)
    {
        decimal subtotal = unitPrice * quantity;
        decimal profit = (unitPrice - costPerUnit) * quantity;
        SalePaymentPlan payment = SalePaymentPlan.Resolve(paymentType, subtotal, paidAmount, paidVia);

        return new Sale(
            productId,
            isManual: false,
            productName,
            category,
            quantity,
            unitPrice,
            subtotal,
            subtotal,
            costPerUnit,
            purchasePricePerUnit,
            profit,
            paymentType,
            // Any payment type may carry a customer; only a remaining balance affects their debt (handler's rule).
            customerId,
            soldByUserId,
            soldByName,
            DateTime.UtcNow,
            // A catalogued sale takes its cost from the product, so it carries no free-form expense lines.
            Array.Empty<SaleExpenseItem>(),
            payment.PaidAmount,
            payment.PaidVia);
    }

    /// <summary>
    /// A free-form sale: no catalogued product, so no stock is moved. The seller supplies the name and may
    /// pass the unit cost if known — pass null when it is not, and <see cref="Profit"/> stays null so the
    /// sale's revenue is still recorded while its gain is reported as unknown. Category is optional.
    /// <paramref name="expenseItems"/> are stored purely for documentation and never alter the cost/profit,
    /// which the caller has already computed.
    /// </summary>
    public static Sale CreateManual(
        string productName,
        string? category,
        int quantity,
        decimal unitPrice,
        decimal? costPerUnit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        IReadOnlyList<SaleExpenseItem>? expenseItems = null,
        decimal? purchasePricePerUnit = null,
        decimal? paidAmount = null,
        PaymentType? paidVia = null)
    {
        decimal subtotal = unitPrice * quantity;
        decimal? profit = costPerUnit is { } cost
            ? (unitPrice - cost) * quantity
            : null;
        SalePaymentPlan payment = SalePaymentPlan.Resolve(paymentType, subtotal, paidAmount, paidVia);

        return new Sale(
            productId: null,
            isManual: true,
            productName,
            category,
            quantity,
            unitPrice,
            subtotal,
            subtotal,
            costPerUnit,
            purchasePricePerUnit,
            profit,
            paymentType,
            customerId,
            soldByUserId,
            soldByName,
            DateTime.UtcNow,
            expenseItems ?? Array.Empty<SaleExpenseItem>(),
            payment.PaidAmount,
            payment.PaidVia);
    }

    /// <summary>
    /// Re-applies a catalogued sale's values in place after its old effects were reversed (the "reapply" half
    /// of an update). Recomputes the money and profit exactly like <see cref="Create"/> but keeps this row's
    /// identity, sale <see cref="Date"/> and seller — an update never rewrites when or by whom the sale was made.
    /// </summary>
    public void ReviseCatalogued(
        Guid productId,
        string productName,
        string? category,
        int quantity,
        decimal unitPrice,
        decimal costPerUnit,
        decimal purchasePricePerUnit,
        PaymentType paymentType,
        Guid? customerId,
        decimal? paidAmount = null,
        PaymentType? paidVia = null)
    {
        ProductId = productId;
        IsManual = false;
        ProductName = productName;
        Category = category;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = unitPrice * quantity;
        TotalAmount = Subtotal;
        CostPerUnit = costPerUnit;
        PurchasePricePerUnit = purchasePricePerUnit;
        Profit = (unitPrice - costPerUnit) * quantity;
        PaymentType = paymentType;
        CustomerId = customerId;
        ExpenseItems = Array.Empty<SaleExpenseItem>();
        ApplyPayment(paymentType, paidAmount, paidVia);
    }

    /// <summary>
    /// Re-applies a free-form sale's values in place after its old effects were reversed. Mirrors
    /// <see cref="CreateManual"/> — profit stays null when the cost is unknown — while preserving this row's
    /// identity, sale <see cref="Date"/> and seller. <paramref name="purchasePricePerUnit"/> is deliberately
    /// required (no default): an edit rewrites the whole row, so the caller must state the purchase price
    /// each time rather than silently dropping the stored one.
    /// </summary>
    public void ReviseManual(
        string productName,
        string? category,
        int quantity,
        decimal unitPrice,
        decimal? costPerUnit,
        PaymentType paymentType,
        Guid? customerId,
        IReadOnlyList<SaleExpenseItem> expenseItems,
        decimal? purchasePricePerUnit,
        decimal? paidAmount = null,
        PaymentType? paidVia = null)
    {
        ProductId = null;
        IsManual = true;
        ProductName = productName;
        Category = category;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = unitPrice * quantity;
        TotalAmount = Subtotal;
        CostPerUnit = costPerUnit;
        PurchasePricePerUnit = purchasePricePerUnit;
        Profit = costPerUnit is { } cost ? (unitPrice - cost) * quantity : null;
        PaymentType = paymentType;
        CustomerId = customerId;
        ExpenseItems = expenseItems;
        ApplyPayment(paymentType, paidAmount, paidVia);
    }

    /// <summary>
    /// Sets <see cref="PaidAmount"/>/<see cref="PaidVia"/> through <see cref="SalePaymentPlan"/> — the same
    /// rule the CreateSale/UpdateSale handlers resolve with, so a revise (or a factory call that omits the
    /// fields) can never drift from it: a Credit sale defaults to nothing paid, everything else to paid in
    /// full. Only the money is taken from the plan; <see cref="PaymentType"/> is the caller's already-resolved
    /// decision and is never rewritten here.
    /// </summary>
    private void ApplyPayment(PaymentType paymentType, decimal? paidAmount, PaymentType? paidVia)
    {
        SalePaymentPlan payment = SalePaymentPlan.Resolve(paymentType, TotalAmount, paidAmount, paidVia);
        PaidAmount = payment.PaidAmount;
        PaidVia = payment.PaidVia;
    }
}
