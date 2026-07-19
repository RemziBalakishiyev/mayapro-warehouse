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
public sealed class Sale : Entity
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
        decimal discount,
        decimal totalAmount,
        decimal? costPerUnit,
        decimal? profit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        DateTime date,
        IReadOnlyList<SaleExpenseItem> expenseItems)
    {
        ProductId = productId;
        IsManual = isManual;
        ProductName = productName;
        Category = category;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = subtotal;
        Discount = discount;
        TotalAmount = totalAmount;
        CostPerUnit = costPerUnit;
        Profit = profit;
        PaymentType = paymentType;
        CustomerId = customerId;
        SoldByUserId = soldByUserId;
        SoldByName = soldByName;
        Date = date;
        ExpenseItems = expenseItems;
    }

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

    /// <summary>Before discount: <see cref="UnitPrice"/> × <see cref="Quantity"/>.</summary>
    public decimal Subtotal { get; private set; }

    public decimal Discount { get; private set; }

    /// <summary>Net after discount: <see cref="Subtotal"/> − <see cref="Discount"/>.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Real cost per unit at sale time (snapshot). Null on a manual sale whose cost is unknown.</summary>
    public decimal? CostPerUnit { get; private set; }

    /// <summary>
    /// (<see cref="UnitPrice"/> − <see cref="CostPerUnit"/>) × <see cref="Quantity"/> − <see cref="Discount"/>.
    /// Null when the cost is unknown (a manual sale with no cost) — the gain is genuinely unknown, so reports
    /// exclude it rather than counting it as zero profit.
    /// </summary>
    public decimal? Profit { get; private set; }

    public PaymentType PaymentType { get; private set; }

    /// <summary>Set for credit (Nisyə) sales; otherwise null.</summary>
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
        decimal discount,
        decimal costPerUnit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName)
    {
        decimal subtotal = unitPrice * quantity;
        decimal totalAmount = subtotal - discount;
        decimal profit = (unitPrice - costPerUnit) * quantity - discount;

        return new Sale(
            productId,
            isManual: false,
            productName,
            category,
            quantity,
            unitPrice,
            subtotal,
            discount,
            totalAmount,
            costPerUnit,
            profit,
            paymentType,
            // Only credit sales carry a customer, matching the frontend rule.
            paymentType == PaymentType.Credit ? customerId : null,
            soldByUserId,
            soldByName,
            DateTime.UtcNow,
            // A catalogued sale takes its cost from the product, so it carries no free-form expense lines.
            Array.Empty<SaleExpenseItem>());
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
        decimal discount,
        decimal? costPerUnit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        IReadOnlyList<SaleExpenseItem>? expenseItems = null)
    {
        decimal subtotal = unitPrice * quantity;
        decimal totalAmount = subtotal - discount;
        decimal? profit = costPerUnit is { } cost
            ? (unitPrice - cost) * quantity - discount
            : null;

        return new Sale(
            productId: null,
            isManual: true,
            productName,
            category,
            quantity,
            unitPrice,
            subtotal,
            discount,
            totalAmount,
            costPerUnit,
            profit,
            paymentType,
            paymentType == PaymentType.Credit ? customerId : null,
            soldByUserId,
            soldByName,
            DateTime.UtcNow,
            expenseItems ?? Array.Empty<SaleExpenseItem>());
    }
}
