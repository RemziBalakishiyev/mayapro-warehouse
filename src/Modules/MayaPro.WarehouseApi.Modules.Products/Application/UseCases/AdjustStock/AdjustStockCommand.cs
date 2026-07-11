namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.AdjustStock;

/// <summary>
/// A manual stock correction: a signed <see cref="Delta"/> (positive adds, negative removes) with an
/// optional note. Stock never drops below zero. <see cref="Id"/> comes from the route.
/// </summary>
public sealed record AdjustStockCommand(Guid Id, int Delta, string? Note);
