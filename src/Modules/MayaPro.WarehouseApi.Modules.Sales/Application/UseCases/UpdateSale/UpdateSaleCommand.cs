using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;

/// <summary>
/// Input for revising a sale. Same shape as creating one plus the <see cref="Id"/> from the route; the
/// sale's date and seller are preserved by the update, only its values (product/quantity/price/discount/
/// payment/customer) change. Fields follow the create rules: a catalogued sale sets <see cref="ProductId"/>
/// (cost/category come from the product); a free-form sale leaves it null and supplies <see cref="ProductName"/>.
/// </summary>
public sealed record UpdateSaleCommand(
    Guid Id,
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
