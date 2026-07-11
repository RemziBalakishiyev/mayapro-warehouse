using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>Domain unit tests for <see cref="Sale"/> — the money and profit computation.</summary>
public sealed class SaleTests
{
    [Fact]
    public void Create_Computes_Subtotal_Total_And_Profit()
    {
        // (20 − 12) × 3 − 5 = 19; subtotal 60; total 55.
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            quantity: 3,
            unitPrice: 20m,
            discount: 5m,
            costPerUnit: 12m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: Guid.NewGuid(),
            soldByName: "Satıcı");

        Assert.Equal(60m, sale.Subtotal);
        Assert.Equal(55m, sale.TotalAmount);
        Assert.Equal(19m, sale.Profit);
    }

    [Fact]
    public void Create_Drops_Customer_For_Non_Credit_Sale()
    {
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            quantity: 1,
            unitPrice: 10m,
            discount: 0m,
            costPerUnit: 5m,
            paymentType: PaymentType.Cash,
            customerId: Guid.NewGuid(), // provided, but a cash sale keeps no customer
            soldByUserId: null,
            soldByName: "Satıcı");

        Assert.Null(sale.CustomerId);
    }
}
