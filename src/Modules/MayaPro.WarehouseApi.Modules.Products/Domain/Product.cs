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
        IReadOnlyList<ProductAttribute> attributes,
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
        IReadOnlyList<ProductExpenseItem> expenses)
    {
        Name = name;
        Category = category;
        Attributes = attributes;
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

    /// <summary>
    /// Free-text category name. A string snapshot (not a foreign key to <see cref="Category"/>) so an
    /// existing product is never broken when the managed category list is renamed or trimmed.
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Dynamic attributes (name/value pairs), replacing the old fixed Size/Color/Model columns. Persisted
    /// as a JSON array on this row via a value converter.
    /// </summary>
    public IReadOnlyList<ProductAttribute> Attributes { get; private set; } = new List<ProductAttribute>();

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

    /// <summary>
    /// Free-form batch expense lines (name/amount), replacing the old fixed Transport/Labor/… buckets.
    /// Persisted as a JSON array on this row via a value converter.
    /// </summary>
    public IReadOnlyList<ProductExpenseItem> Expenses { get; private set; } = ProductExpenses.Empty;

    /// <summary>Computed real cost of one unit; recalculated whenever inputs change.</summary>
    public decimal RealCostPerUnit { get; private set; }

    public static Product Create(
        string name,
        string category,
        IReadOnlyList<ProductAttribute> attributes,
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
        IReadOnlyList<ProductExpenseItem> expenses) =>
        new(name, category, attributes, barcode, image, note, purchasePrice, salePrice,
            quantity, minStock, currency, supplierId, location, store, warehouse, shelf, box, expenses);

    /// <summary>
    /// Applies an edit. <see cref="InitialQuantity"/> stays fixed (set at creation); everything else —
    /// including current stock — may change. Real cost is recomputed from the new inputs.
    /// </summary>
    public void Update(
        string name,
        string category,
        IReadOnlyList<ProductAttribute> attributes,
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
        IReadOnlyList<ProductExpenseItem> expenses)
    {
        Name = name;
        Category = category;
        Attributes = attributes;
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

    /// <summary>Returns reserved stock (a deleted or revised sale) — the inverse of <see cref="TryDecreaseStock"/>.</summary>
    public void IncreaseStock(int quantity) => Quantity += quantity;

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

    /// <summary>
    /// Attaches a named batch expense line and recomputes the real cost. When a line with the same name
    /// already exists (case-insensitive), the amount is added to it; otherwise a new line is appended.
    /// </summary>
    public void AddExpense(string name, decimal amount)
    {
        Expenses = ProductExpenses.Add(Expenses, name, amount);
        RecalculateRealCost();
    }

    /// <summary>
    /// Removes a previously-added batch expense amount from the matching named line (case-insensitive) and
    /// recomputes the real cost — the inverse of <see cref="AddExpense"/>. Used when a product-linked expense
    /// is deleted or revised. An unknown line is a no-op.
    /// </summary>
    public void RemoveExpense(string name, decimal amount)
    {
        Expenses = ProductExpenses.Remove(Expenses, name, amount);
        RecalculateRealCost();
    }

    private void RecalculateRealCost() =>
        RealCostPerUnit = CalculateRealCost(PurchasePrice, InitialQuantity, Expenses);

    /// <summary>
    /// Real cost of one unit: purchase price plus the batch expenses spread over the initial quantity,
    /// rounded to money precision. With no initial quantity there is nothing to spread over, so the real
    /// cost is just the purchase price.
    /// </summary>
    public static decimal CalculateRealCost(
        decimal purchasePrice,
        int initialQuantity,
        IReadOnlyList<ProductExpenseItem> expenses)
    {
        decimal perUnit = initialQuantity > 0
            ? purchasePrice + ProductExpenses.Total(expenses) / initialQuantity
            : purchasePrice;

        return Math.Round(perUnit, 2, MidpointRounding.AwayFromZero);
    }
}
