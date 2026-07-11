namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// Input for creating a sale. <see cref="PaymentType"/> is a frontend code (<c>"Nağd" | "Kart" | "Nisyə"</c>);
/// <see cref="CustomerId"/> is required for credit (Nisyə) sales.
/// </summary>
public sealed record CreateSaleCommand(
    Guid ProductId,
    int Quantity,
    decimal SalePrice,
    decimal Discount,
    string PaymentType,
    Guid? CustomerId,
    string? Note);
