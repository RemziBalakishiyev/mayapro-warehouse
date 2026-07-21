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
            category: "Şalvar",
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
        Assert.Equal("Şalvar", sale.Category);
        Assert.False(sale.IsManual);
    }

    [Fact]
    public void Create_Drops_Customer_For_Non_Credit_Sale()
    {
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            category: "Test",
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

    [Fact]
    public void CreateManual_Without_Cost_Leaves_Profit_Unknown()
    {
        // No cost supplied → the gain is genuinely unknown, so Profit stays null (no phantom gain written).
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal",
            category: null,
            quantity: 2,
            unitPrice: 15m,
            discount: 0m,
            costPerUnit: null,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: Guid.NewGuid(),
            soldByName: "Satıcı");

        Assert.True(sale.IsManual);
        Assert.Null(sale.ProductId);
        Assert.Null(sale.CostPerUnit);
        Assert.Null(sale.Profit);
        Assert.Null(sale.Category);
        Assert.Equal(30m, sale.Subtotal);      // revenue is still recorded
        Assert.Equal(30m, sale.TotalAmount);
    }

    [Fact]
    public void CreateManual_With_Cost_Computes_Profit_Like_A_Normal_Sale()
    {
        // (20 − 12) × 3 − 5 = 19 — same formula as a catalogued sale once the cost is known.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal",
            category: "Aksesuar",
            quantity: 3,
            unitPrice: 20m,
            discount: 5m,
            costPerUnit: 12m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: null,
            soldByName: "Satıcı");

        Assert.True(sale.IsManual);
        Assert.Null(sale.ProductId);
        Assert.Equal(12m, sale.CostPerUnit);
        Assert.Equal(19m, sale.Profit);
        Assert.Equal(55m, sale.TotalAmount);
        Assert.Equal("Aksesuar", sale.Category);
    }

    [Fact]
    public void CreateManual_Keeps_Customer_Only_For_Credit()
    {
        Guid customerId = Guid.NewGuid();

        Sale credit = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 1, unitPrice: 10m, discount: 0m, costPerUnit: null,
            paymentType: PaymentType.Credit, customerId: customerId, soldByUserId: null, soldByName: "Satıcı");
        Sale cash = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 1, unitPrice: 10m, discount: 0m, costPerUnit: null,
            paymentType: PaymentType.Cash, customerId: customerId, soldByUserId: null, soldByName: "Satıcı");

        Assert.Equal(customerId, credit.CustomerId);
        Assert.Null(cash.CustomerId);
    }

    [Fact]
    public void ReviseCatalogued_Recomputes_Money_And_Preserves_Identity_Date_And_Seller()
    {
        Guid seller = Guid.NewGuid();
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(), productName: "Köhnə", category: "Köhnə kateqoriya", quantity: 1,
            unitPrice: 10m, discount: 0m, costPerUnit: 5m, paymentType: PaymentType.Cash,
            customerId: null, soldByUserId: seller, soldByName: "Satıcı");

        Guid originalId = sale.Id;
        DateTime originalDate = sale.Date;
        Guid newProductId = Guid.NewGuid();

        // New values: (20 − 12) × 3 − 5 = 19; subtotal 60; total 55.
        sale.ReviseCatalogued(
            newProductId, "Yeni", "Yeni kateqoriya", quantity: 3, unitPrice: 20m, discount: 5m,
            costPerUnit: 12m, paymentType: PaymentType.Cash, customerId: null);

        Assert.Equal(newProductId, sale.ProductId);
        Assert.Equal("Yeni", sale.ProductName);
        Assert.Equal("Yeni kateqoriya", sale.Category);
        Assert.Equal(60m, sale.Subtotal);
        Assert.Equal(55m, sale.TotalAmount);
        Assert.Equal(19m, sale.Profit);
        Assert.False(sale.IsManual);
        // Identity, sale date and seller are never rewritten by an update.
        Assert.Equal(originalId, sale.Id);
        Assert.Equal(originalDate, sale.Date);
        Assert.Equal(seller, sale.SoldByUserId);
    }

    [Fact]
    public void ReviseManual_Recomputes_And_Drops_Customer_For_Non_Credit()
    {
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(), productName: "Köhnə", category: "K", quantity: 1,
            unitPrice: 10m, discount: 0m, costPerUnit: 5m, paymentType: PaymentType.Credit,
            customerId: Guid.NewGuid(), soldByUserId: null, soldByName: "Satıcı");

        var items = new List<SaleExpenseItem> { new("Yol", 5m) };
        // Cash → the supplied customer is dropped; no cost → profit stays unknown.
        sale.ReviseManual(
            "Əl ilə", category: null, quantity: 2, unitPrice: 15m, discount: 0m, costPerUnit: null,
            paymentType: PaymentType.Cash, customerId: Guid.NewGuid(), expenseItems: items);

        Assert.True(sale.IsManual);
        Assert.Null(sale.ProductId);
        Assert.Null(sale.CustomerId);
        Assert.Null(sale.Profit);
        Assert.Equal(30m, sale.TotalAmount);
        Assert.Equal(new SaleExpenseItem("Yol", 5m), Assert.Single(sale.ExpenseItems));
    }
}
