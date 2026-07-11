using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Products.Domain;

/// <summary>
/// A product in the warehouse. Behaviour-rich entity — no public setters; every state change goes
/// through a method that keeps <see cref="RealCostPerUnit"/> consistent.
/// <para>
/// Real cost = purchase price + (total batch expenses ÷ initial quantity). Expenses are spread over the
/// batch that was originally bought, so <see cref="InitialQuantity"/> — fixed at creation — is always the
/// divisor, matching the frontend seed and the "expense → recompute cost" chain.
/// </para>
/// </summary>
public sealed class Product : Entity
{
    // EF Core constructor.
    private Product() { }

    private Product(
        string name,
        string category,
        string size,
        string color,
        string model,
        string barcode,
        string image,
        string note,
        decimal purchasePrice,
        decimal salePrice,
        int quantity,
        int minStock,
        string currency,
        string supplierId,
        string location,
        string store,
        string warehouse,
        string shelf,
        string box,
        ProductExpenses expenses)
    {
        Name = name;
        Category = category;
        Size = size;
        Color = color;
        Model = model;
        Barcode = barcode;
        Image = image;
        Note = note;
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        Quantity = quantity;
        InitialQuantity = quantity;
        MinStock = minStock;
        Currency = currency;
        SupplierId = supplierId;
        Location = location;
        Store = store;
        Warehouse = warehouse;
        Shelf = shelf;
        Box = box;
        Expenses = expenses;
        RecalculateRealCost();
    }

    public string Name { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string Color { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string Barcode { get; private set; } = string.Empty;

    public string Image { get; private set; } = string.Empty;

    public string Note { get; private set; } = string.Empty;

    public decimal PurchasePrice { get; private set; }

    public decimal SalePrice { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Quantity at creation; fixed for the product's life. Divisor for the real-cost formula.</summary>
    public int InitialQuantity { get; private set; }

    public int MinStock { get; private set; }

    public string Currency { get; private set; } = "AZN";

    /// <summary>Reference to the owning supplier (cross-module id; no FK by design).</summary>
    public string SupplierId { get; private set; } = string.Empty;

    /// <summary>Compact address, e.g. "Anbar A / Rəf 3 / Qutu 12".</summary>
    public string Location { get; private set; } = string.Empty;

    public string Store { get; private set; } = string.Empty;

    public string Warehouse { get; private set; } = string.Empty;

    public string Shelf { get; private set; } = string.Empty;

    public string Box { get; private set; } = string.Empty;

    public ProductExpenses Expenses { get; private set; } = ProductExpenses.Empty();

    /// <summary>Computed real cost of one unit; recalculated whenever inputs change.</summary>
    public decimal RealCostPerUnit { get; private set; }

    public static Product Create(
        string name,
        string category,
        string size,
        string color,
        string model,
        string barcode,
        string image,
        string note,
        decimal purchasePrice,
        decimal salePrice,
        int quantity,
        int minStock,
        string currency,
        string supplierId,
        string location,
        string store,
        string warehouse,
        string shelf,
        string box,
        ProductExpenses expenses) =>
        new(name, category, size, color, model, barcode, image, note, purchasePrice, salePrice,
            quantity, minStock, currency, supplierId, location, store, warehouse, shelf, box, expenses);

    /// <summary>
    /// Applies an edit. <see cref="InitialQuantity"/> stays fixed (set at creation); everything else —
    /// including current stock — may change. Real cost is recomputed from the new inputs.
    /// </summary>
    public void Update(
        string name,
        string category,
        string size,
        string color,
        string model,
        string barcode,
        string image,
        string note,
        decimal purchasePrice,
        decimal salePrice,
        int quantity,
        int minStock,
        string currency,
        string supplierId,
        string location,
        string store,
        string warehouse,
        string shelf,
        string box,
        ProductExpenses expenses)
    {
        Name = name;
        Category = category;
        Size = size;
        Color = color;
        Model = model;
        Barcode = barcode;
        Image = image;
        Note = note;
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        Quantity = quantity;
        MinStock = minStock;
        Currency = currency;
        SupplierId = supplierId;
        Location = location;
        Store = store;
        Warehouse = warehouse;
        Shelf = shelf;
        Box = box;
        Expenses = expenses;
        RecalculateRealCost();
    }

    /// <summary>Manual stock correction by a signed delta. Never drops below zero.</summary>
    public void AdjustStock(int delta) => Quantity = Math.Max(0, Quantity + delta);

    /// <summary>
    /// Reserves stock for a sale. Fails with <see cref="ProductErrors.InsufficientStock"/> if the
    /// requested quantity exceeds what is on hand; otherwise decreases stock and succeeds.
    /// </summary>
    public Result TryDecreaseStock(int quantity)
    {
        if (quantity > Quantity)
            return Result.Failure(ProductErrors.InsufficientStock);

        Quantity -= quantity;
        return Result.Success();
    }

    /// <summary>Attaches a batch expense to the given bucket and recomputes the real cost.</summary>
    public void AddExpense(ProductExpenseKind kind, decimal amount)
    {
        Expenses.Add(kind, amount);
        RecalculateRealCost();
    }

    private void RecalculateRealCost() =>
        RealCostPerUnit = CalculateRealCost(PurchasePrice, InitialQuantity, Expenses);

    /// <summary>
    /// Real cost of one unit: purchase price plus the batch expenses spread over the initial quantity,
    /// rounded to money precision. With no initial quantity there is nothing to spread over, so the real
    /// cost is just the purchase price.
    /// </summary>
    public static decimal CalculateRealCost(decimal purchasePrice, int initialQuantity, ProductExpenses expenses)
    {
        decimal perUnit = initialQuantity > 0
            ? purchasePrice + expenses.Total / initialQuantity
            : purchasePrice;

        return Math.Round(perUnit, 2, MidpointRounding.AwayFromZero);
    }
}
