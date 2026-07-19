using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// Input for creating a sale. <see cref="PaymentType"/> is a frontend code (<c>"Nağd" | "Kart" | "Nisyə"</c>);
/// <see cref="CustomerId"/> is required for credit (Nisyə) sales.
/// <para>
/// A normal sale sets <see cref="ProductId"/> and the item is taken from the catalogue — category is snapshotted
/// from the product (any <see cref="Category"/> on the command is ignored). A free-form ("manual") sale leaves
/// <see cref="ProductId"/> null and supplies <see cref="ProductName"/> (required) by hand;
/// <see cref="CostPerUnit"/> and <see cref="Category"/> are optional. Cost fields are ignored when
/// <see cref="ProductId"/> is set.
/// </para>
/// <para>
/// <see cref="ExpenseItems"/> is optional and only meaningful for a free-form sale: it documents how the
/// seller-supplied <see cref="CostPerUnit"/> was worked out. It is stored as-is and never recomputes the
/// cost/profit; it is ignored for catalogued sales.
/// </para>
/// </summary>
public sealed record CreateSaleCommand(
    Guid? ProductId,
    int Quantity,
    decimal SalePrice,
    decimal Discount,
    string PaymentType,
    Guid? CustomerId,
    string? Note,
    string? ProductName = null,
    decimal? CostPerUnit = null,
    string? Category = null,
    IReadOnlyList<SaleExpenseItemDto>? ExpenseItems = null);
