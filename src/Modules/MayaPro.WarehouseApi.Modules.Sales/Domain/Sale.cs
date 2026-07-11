using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// A completed sale. Snapshots the product name and its real cost at sale time, so historical profit is
/// stable even if the product's cost later changes. Amounts are computed once in <see cref="Create"/>.
/// </summary>
public sealed class Sale : Entity
{
    // EF Core constructor.
    private Sale() { }

    private Sale(
        Guid productId,
        string productName,
        int quantity,
        decimal unitPrice,
        decimal subtotal,
        decimal discount,
        decimal totalAmount,
        decimal costPerUnit,
        decimal profit,
        PaymentType paymentType,
        Guid? customerId,
        Guid? soldByUserId,
        string soldByName,
        DateTime date)
    {
        ProductId = productId;
        ProductName = productName;
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
    }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    /// <summary>Before discount: <see cref="UnitPrice"/> × <see cref="Quantity"/>.</summary>
    public decimal Subtotal { get; private set; }

    public decimal Discount { get; private set; }

    /// <summary>Net after discount: <see cref="Subtotal"/> − <see cref="Discount"/>.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Real cost per unit at sale time (snapshot).</summary>
    public decimal CostPerUnit { get; private set; }

    /// <summary>(<see cref="UnitPrice"/> − <see cref="CostPerUnit"/>) × <see cref="Quantity"/> − <see cref="Discount"/>.</summary>
    public decimal Profit { get; private set; }

    public PaymentType PaymentType { get; private set; }

    /// <summary>Set for credit (Nisyə) sales; otherwise null.</summary>
    public Guid? CustomerId { get; private set; }

    public Guid? SoldByUserId { get; private set; }

    public string SoldByName { get; private set; } = string.Empty;

    public DateTime Date { get; private set; }

    public static Sale Create(
        Guid productId,
        string productName,
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
            productName,
            quantity,
            unitPrice,
            subtotal,
            discount,
            totalAmount,
            costPerUnit,
            profit,
            paymentType,
            // Only credit sales carry a customer, matching the frontend rule.
            paymentType == PaymentType.Nisye ? customerId : null,
            soldByUserId,
            soldByName,
            DateTime.UtcNow);
    }
}
