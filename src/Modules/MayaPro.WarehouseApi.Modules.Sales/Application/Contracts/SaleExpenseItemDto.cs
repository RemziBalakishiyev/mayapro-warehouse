namespace MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

/// <summary>
/// A free-form sale expense line on the wire: <c>{ "name": "Yol pulu", "amount": 5 }</c>. Used both on the
/// <c>CreateSale</c> request (documentation for a manual sale) and on the sale DTOs that echo it back.
/// </summary>
public sealed record SaleExpenseItemDto(string Name, decimal Amount);
