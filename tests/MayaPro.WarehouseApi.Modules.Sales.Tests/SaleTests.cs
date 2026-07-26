using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Sales.Domain;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>Domain unit tests for <see cref="Sale"/> — the money and profit computation.</summary>
public sealed class SaleTests
{
    [Fact]
    public void Create_Computes_Subtotal_Total_And_Profit()
    {
        // (20 − 12) × 3 = 24; subtotal/total 60.
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            category: "Şalvar",
            quantity: 3,
            unitPrice: 20m,
            costPerUnit: 12m,
            purchasePricePerUnit: 8m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: Guid.NewGuid(),
            soldByName: "Satıcı");

        Assert.Equal(60m, sale.Subtotal);
        Assert.Equal(60m, sale.TotalAmount);
        Assert.Equal(24m, sale.Profit);
        Assert.Equal("Şalvar", sale.Category);
        Assert.False(sale.IsManual);
    }

    [Fact]
    public void Create_Keeps_Customer_For_Non_Credit_Sale()
    {
        Guid customerId = Guid.NewGuid();

        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            category: "Test",
            quantity: 1,
            unitPrice: 10m,
            costPerUnit: 5m,
            purchasePricePerUnit: 3m,
            paymentType: PaymentType.Cash,
            customerId: customerId, // optional on cash/card — kept for purchase history, debt untouched
            soldByUserId: null,
            soldByName: "Satıcı");

        Assert.Equal(customerId, sale.CustomerId);
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
        // (20 − 12) × 3 = 24 — same formula as a catalogued sale once the cost is known.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal",
            category: "Aksesuar",
            quantity: 3,
            unitPrice: 20m,
            costPerUnit: 12m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: null,
            soldByName: "Satıcı");

        Assert.True(sale.IsManual);
        Assert.Null(sale.ProductId);
        Assert.Equal(12m, sale.CostPerUnit);
        Assert.Equal(24m, sale.Profit);
        Assert.Equal(60m, sale.TotalAmount);
        Assert.Equal("Aksesuar", sale.Category);
    }

    [Fact]
    public void CreateManual_Keeps_Customer_For_Every_Payment_Type()
    {
        Guid customerId = Guid.NewGuid();

        Sale credit = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 1, unitPrice: 10m, costPerUnit: null,
            paymentType: PaymentType.Credit, customerId: customerId, soldByUserId: null, soldByName: "Satıcı");
        Sale cash = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 1, unitPrice: 10m, costPerUnit: null,
            paymentType: PaymentType.Cash, customerId: customerId, soldByUserId: null, soldByName: "Satıcı");

        Assert.Equal(customerId, credit.CustomerId);
        Assert.Equal(customerId, cash.CustomerId);
    }

    [Fact]
    public void ReviseCatalogued_Recomputes_Money_And_Preserves_Identity_Date_And_Seller()
    {
        Guid seller = Guid.NewGuid();
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(), productName: "Köhnə", category: "Köhnə kateqoriya", quantity: 1,
            unitPrice: 10m, costPerUnit: 5m, purchasePricePerUnit: 4m, paymentType: PaymentType.Cash,
            customerId: null, soldByUserId: seller, soldByName: "Satıcı");

        Guid originalId = sale.Id;
        DateTime originalDate = sale.Date;
        Guid newProductId = Guid.NewGuid();

        // New values: (20 − 12) × 3 = 24; subtotal/total 60.
        sale.ReviseCatalogued(
            newProductId, "Yeni", "Yeni kateqoriya", quantity: 3, unitPrice: 20m,
            costPerUnit: 12m, purchasePricePerUnit: 9m, paymentType: PaymentType.Cash, customerId: null);

        Assert.Equal(newProductId, sale.ProductId);
        Assert.Equal("Yeni", sale.ProductName);
        Assert.Equal("Yeni kateqoriya", sale.Category);
        Assert.Equal(60m, sale.Subtotal);
        Assert.Equal(60m, sale.TotalAmount);
        Assert.Equal(24m, sale.Profit);
        Assert.Equal(9m, sale.PurchasePricePerUnit);
        Assert.False(sale.IsManual);
        // Identity, sale date and seller are never rewritten by an update.
        Assert.Equal(originalId, sale.Id);
        Assert.Equal(originalDate, sale.Date);
        Assert.Equal(seller, sale.SoldByUserId);
    }

    [Fact]
    public void ReviseManual_Recomputes_And_Keeps_Customer_For_Non_Credit()
    {
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(), productName: "Köhnə", category: "K", quantity: 1,
            unitPrice: 10m, costPerUnit: 5m, purchasePricePerUnit: 4m, paymentType: PaymentType.Credit,
            customerId: Guid.NewGuid(), soldByUserId: null, soldByName: "Satıcı");

        Guid newCustomerId = Guid.NewGuid();
        var items = new List<SaleExpenseItem> { new("Yol", 5m) };
        // Cash keeps the supplied customer too (debt untouched); no cost → profit stays unknown.
        // Turning a catalogued sale into a free-form one drops the product's purchase-price snapshot: the
        // sale no longer points at a product, so nothing vouches for that figure any more.
        sale.ReviseManual(
            "Əl ilə", category: null, quantity: 2, unitPrice: 15m, costPerUnit: null,
            paymentType: PaymentType.Cash, customerId: newCustomerId, expenseItems: items,
            purchasePricePerUnit: null);

        Assert.True(sale.IsManual);
        Assert.Null(sale.ProductId);
        Assert.Equal(newCustomerId, sale.CustomerId);
        Assert.Null(sale.Profit);
        Assert.Null(sale.PurchasePricePerUnit); // the old catalogued snapshot is not left behind
        Assert.Equal(30m, sale.TotalAmount);
        Assert.Equal(new SaleExpenseItem("Yol", 5m), Assert.Single(sale.ExpenseItems));
    }

    // ── PurchasePricePerUnit (BE#1) ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateManual_Separates_PurchasePrice_From_ComputedCost()
    {
        // TC-1: purchase price 100 + Σexpenses 50 over 2 units → costPerUnit 125 (caller-computed);
        // purchasePricePerUnit stays 100 — the two figures are stored side by side, one never overwrites
        // the other. Profit uses CostPerUnit, unaffected by the presence of PurchasePricePerUnit.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal",
            category: null,
            quantity: 2,
            unitPrice: 200m,
            costPerUnit: 125m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: null,
            soldByName: "Satıcı",
            expenseItems: new List<SaleExpenseItem> { new("Xərc", 50m) },
            purchasePricePerUnit: 100m);

        Assert.Equal(100m, sale.PurchasePricePerUnit);
        Assert.Equal(125m, sale.CostPerUnit);
        Assert.Equal(150m, sale.Profit); // (200 − 125) × 2
        Assert.Equal(400m, sale.Subtotal);
        Assert.Equal(400m, sale.TotalAmount);
    }

    [Fact]
    public void Create_Snapshots_PurchasePrice_Separately_From_RealCost()
    {
        // TC-2: product PurchasePrice=80, RealCostPerUnit=95 (purchase price + spread expenses); the sale
        // snapshots both — PurchasePricePerUnit from the product's purchase price, CostPerUnit unchanged
        // from the product's real cost.
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(),
            productName: "Mal",
            category: "Kateqoriya",
            quantity: 3,
            unitPrice: 150m,
            costPerUnit: 95m,
            purchasePricePerUnit: 80m,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: null,
            soldByName: "Satıcı");

        Assert.Equal(80m, sale.PurchasePricePerUnit);
        Assert.Equal(95m, sale.CostPerUnit);
        Assert.Equal(165m, sale.Profit); // (150 − 95) × 3
    }

    [Fact]
    public void CreateManual_Without_PurchasePrice_Is_Null_Safe()
    {
        // TC-3: neither cost nor purchase price supplied — no exception, both null, profit stays unknown.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal",
            category: null,
            quantity: 1,
            unitPrice: 50m,
            costPerUnit: null,
            paymentType: PaymentType.Cash,
            customerId: null,
            soldByUserId: null,
            soldByName: "Satıcı",
            purchasePricePerUnit: null);

        Assert.Null(sale.PurchasePricePerUnit);
        Assert.Null(sale.CostPerUnit);
        Assert.Null(sale.Profit);
    }

    [Fact]
    public void ReviseCatalogued_Resnapshots_PurchasePrice()
    {
        // TC-9: an update re-snapshots the product's *current* purchase price, independent of the value the
        // sale was originally created with.
        Sale sale = Sale.Create(
            productId: Guid.NewGuid(), productName: "Mal", category: "K", quantity: 1,
            unitPrice: 100m, costPerUnit: 80m, purchasePricePerUnit: 80m, paymentType: PaymentType.Cash,
            customerId: null, soldByUserId: null, soldByName: "Satıcı");

        Assert.Equal(80m, sale.PurchasePricePerUnit);

        // Product's purchase price changed to 90 since the sale was made; the update snapshots it afresh.
        sale.ReviseCatalogued(
            sale.ProductId!.Value, "Mal", "K", quantity: 1, unitPrice: 100m,
            costPerUnit: 85m, purchasePricePerUnit: 90m, paymentType: PaymentType.Cash, customerId: null);

        Assert.Equal(90m, sale.PurchasePricePerUnit);
    }

    [Fact]
    public void ReviseManual_Rewrites_PurchasePrice_And_Leaves_Cost_Independent()
    {
        // AC-6, free-form half: the edited command's value replaces the stored one outright — and clearing it
        // (null) is a legitimate edit, not a reason to keep the previous figure.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 2, unitPrice: 200m, costPerUnit: 125m,
            paymentType: PaymentType.Cash, customerId: null, soldByUserId: null, soldByName: "Satıcı",
            expenseItems: null, purchasePricePerUnit: 100m);

        sale.ReviseManual(
            "Əl ilə mal", category: null, quantity: 2, unitPrice: 200m, costPerUnit: 130m,
            paymentType: PaymentType.Cash, customerId: null,
            expenseItems: Array.Empty<SaleExpenseItem>(), purchasePricePerUnit: 120m);

        Assert.Equal(120m, sale.PurchasePricePerUnit);
        Assert.Equal(130m, sale.CostPerUnit);        // the two figures move independently
        Assert.Equal(140m, sale.Profit);             // (200 − 130) × 2 — still driven by CostPerUnit only

        sale.ReviseManual(
            "Əl ilə mal", category: null, quantity: 2, unitPrice: 200m, costPerUnit: 130m,
            paymentType: PaymentType.Cash, customerId: null,
            expenseItems: Array.Empty<SaleExpenseItem>(), purchasePricePerUnit: null);

        Assert.Null(sale.PurchasePricePerUnit);
        Assert.Equal(130m, sale.CostPerUnit);        // clearing the purchase price never touches the cost
    }

    [Fact]
    public void ToDto_Carries_PurchasePricePerUnit()
    {
        // TC-8: the wire DTO exposes purchasePricePerUnit for both the summary and detail shapes.
        Sale sale = Sale.CreateManual(
            productName: "Əl ilə mal", category: null, quantity: 2, unitPrice: 200m, costPerUnit: 125m,
            paymentType: PaymentType.Cash, customerId: null, soldByUserId: null, soldByName: "Satıcı",
            purchasePricePerUnit: 100m);

        SaleDto dto = sale.ToDto();
        SaleDetailDto detail = sale.ToDetailDto(customerName: null, currentProductName: null);

        Assert.Equal(100m, dto.PurchasePricePerUnit);
        Assert.Equal(100m, detail.PurchasePricePerUnit);
    }
}
