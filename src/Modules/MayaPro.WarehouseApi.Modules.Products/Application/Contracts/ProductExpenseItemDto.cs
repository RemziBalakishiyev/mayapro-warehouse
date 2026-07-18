namespace MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

/// <summary>
/// A free-form product expense line on the wire: <c>{ "name": "Yol pulu", "amount": 240 }</c>.
/// Replaces the old fixed <c>yol/fehle/yer/paket/diger</c> object.
/// </summary>
public sealed record ProductExpenseItemDto(string Name, decimal Amount);
