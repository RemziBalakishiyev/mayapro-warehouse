using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.UpdateSale;

/// <summary>
/// Same rules as creating a sale — an update is a full reverse-and-reapply of the sale's values, so it shares
/// <see cref="SaleWriteValidator{TCommand}"/> outright instead of restating the rules.
/// </summary>
public sealed class UpdateSaleValidator : SaleWriteValidator<UpdateSaleCommand>
{
}
