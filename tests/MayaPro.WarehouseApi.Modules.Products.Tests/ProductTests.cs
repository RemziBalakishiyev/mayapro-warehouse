using MayaPro.WarehouseApi.Modules.Products.Domain;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Domain unit tests for <see cref="Product"/> — the real-cost formula and the stock/expense behaviours
/// that the sales chain will rely on.
/// </summary>
public sealed class ProductTests
{
    [Fact]
    public void RealCost_With_Expenses_Spreads_Batch_Cost_Over_Initial_Quantity()
    {
        // 14 + (240+60+50+30) / 120 = 14 + 380/120 = 17.1666… → 17.17
        Product product = CreateProduct(
            purchasePrice: 14,
            quantity: 120,
            expenses:
            [
                new("Yol pulu", 240),
                new("Fəhlə pulu", 60),
                new("Yer/Anbar xərci", 50),
                new("Paket/Qutu", 30)
            ]);

        Assert.Equal(17.17m, product.RealCostPerUnit);
    }

    [Fact]
    public void RealCost_Without_Expenses_Equals_Purchase_Price()
    {
        Product product = CreateProduct(purchasePrice: 10, quantity: 50, expenses: ProductExpenses.Empty);

        Assert.Equal(10m, product.RealCostPerUnit);
    }

    [Fact]
    public void RealCost_With_Zero_Quantity_Falls_Back_To_Purchase_Price()
    {
        // Nothing to spread the expenses over → real cost is just the purchase price (no divide-by-zero).
        Product product = CreateProduct(
            purchasePrice: 14,
            quantity: 0,
            expenses: [new("Yol pulu", 240), new("Fəhlə pulu", 60)]);

        Assert.Equal(14m, product.RealCostPerUnit);
    }

    [Fact]
    public void TryDecreaseStock_With_Enough_Stock_Succeeds_And_Reduces_Quantity()
    {
        Product product = CreateProduct(quantity: 10);

        var result = product.TryDecreaseStock(4);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, product.Quantity);
    }

    [Fact]
    public void TryDecreaseStock_Beyond_Stock_Fails_And_Leaves_Quantity_Untouched()
    {
        Product product = CreateProduct(quantity: 10);

        var result = product.TryDecreaseStock(11);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.InsufficientStock, result.Error);
        Assert.Equal(10, product.Quantity);
    }

    [Fact]
    public void AddExpense_Increases_Real_Cost()
    {
        Product product = CreateProduct(purchasePrice: 10, quantity: 100, expenses: ProductExpenses.Empty);
        decimal before = product.RealCostPerUnit; // 10.00

        product.AddExpense("Yol", 100); // +100/100 = +1.00

        Assert.Equal(10m, before);
        Assert.Equal(11m, product.RealCostPerUnit);
        Assert.Equal(new ProductExpenseItem("Yol", 100m), Assert.Single(product.Expenses));
    }

    [Fact]
    public void AddExpense_Same_Name_Accumulates_On_Existing_Line()
    {
        Product product = CreateProduct(
            purchasePrice: 10,
            quantity: 100,
            expenses: [new("Yol", 50)]);

        product.AddExpense("yol", 25); // case-insensitive match → 75

        Assert.Equal(new ProductExpenseItem("Yol", 75m), Assert.Single(product.Expenses));
        Assert.Equal(10.75m, product.RealCostPerUnit);
    }

    [Fact]
    public void AddExpense_Different_Name_Appends_New_Line()
    {
        Product product = CreateProduct(
            purchasePrice: 10,
            quantity: 100,
            expenses: [new("Yol", 50)]);

        product.AddExpense("Fəhlə", 50);

        Assert.Equal(2, product.Expenses.Count);
        Assert.Contains(product.Expenses, e => e is { Name: "Yol", Amount: 50m });
        Assert.Contains(product.Expenses, e => e is { Name: "Fəhlə", Amount: 50m });
        Assert.Equal(11m, product.RealCostPerUnit);
    }

    [Fact]
    public void AdjustStock_Never_Drops_Below_Zero()
    {
        Product product = CreateProduct(quantity: 5);

        product.AdjustStock(-10);

        Assert.Equal(0, product.Quantity);
    }

    [Fact]
    public void IncreaseStock_Returns_Reserved_Units()
    {
        // A deleted/revised sale returns its quantity — the inverse of TryDecreaseStock.
        Product product = CreateProduct(quantity: 6);

        product.IncreaseStock(4);

        Assert.Equal(10, product.Quantity);
    }

    [Fact]
    public void RemoveExpense_Reverses_AddExpense_And_Lowers_Real_Cost_Back()
    {
        Product product = CreateProduct(purchasePrice: 10, quantity: 100, expenses: ProductExpenses.Empty);
        product.AddExpense("Yol", 100); // 10 → 11.00

        product.RemoveExpense("Yol", 100); // back to 10.00, line dropped

        Assert.Equal(10m, product.RealCostPerUnit);
        Assert.Empty(product.Expenses);
    }

    [Fact]
    public void RemoveExpense_Partial_Keeps_The_Line_With_The_Remainder()
    {
        Product product = CreateProduct(
            purchasePrice: 10,
            quantity: 100,
            expenses: [new("Yol", 100)]); // 10 + 100/100 = 11.00

        product.RemoveExpense("yol", 40); // case-insensitive → 60 remains → 10.60

        Assert.Equal(new ProductExpenseItem("Yol", 60m), Assert.Single(product.Expenses));
        Assert.Equal(10.60m, product.RealCostPerUnit);
    }

    [Fact]
    public void RemoveExpense_Unknown_Line_Is_A_No_Op()
    {
        Product product = CreateProduct(
            purchasePrice: 10,
            quantity: 100,
            expenses: [new("Yol", 50)]);

        product.RemoveExpense("Fəhlə", 30); // no matching line → unchanged

        Assert.Equal(new ProductExpenseItem("Yol", 50m), Assert.Single(product.Expenses));
        Assert.Equal(10.50m, product.RealCostPerUnit);
    }

    private static Product CreateProduct(
        decimal purchasePrice = 10,
        int quantity = 10,
        IReadOnlyList<ProductExpenseItem>? expenses = null) =>
        Product.Create(
            name: "Test məhsul",
            category: "Test",
            attributes: new List<ProductAttribute> { new("Ölçü", "M"), new("Rəng", "Qara") },
            barcode: "TST001",
            image: string.Empty,
            note: string.Empty,
            purchasePrice: purchasePrice,
            salePrice: 20,
            quantity: quantity,
            minStock: 1,
            currency: "AZN",
            supplierId: "sup_1",
            location: "Anbar A / Rəf 1 / Qutu 1",
            store: "Anbar A",
            warehouse: "Anbar A",
            shelf: "1",
            box: "1",
            expenses: expenses ?? ProductExpenses.Empty);
}
