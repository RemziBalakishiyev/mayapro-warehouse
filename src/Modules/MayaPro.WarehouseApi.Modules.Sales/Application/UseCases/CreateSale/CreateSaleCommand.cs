namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// Input for creating a sale. <see cref="PaymentType"/> is a frontend code (<c>"Nağd" | "Kart" | "Nisyə"</c>);
/// <see cref="CustomerId"/> is required for credit (Nisyə) sales.
/// <para>
/// A normal sale sets <see cref="ProductId"/> and the item is taken from the catalogue. A free-form ("manual")
/// sale leaves <see cref="ProductId"/> null and supplies <see cref="ProductName"/> (required) by hand;
/// <see cref="CostPerUnit"/> is optional — send it if the cost is known, otherwise leave it null and the
/// sale's profit stays unknown. Both fields are ignored when <see cref="ProductId"/> is set.
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
    decimal? CostPerUnit = null);
