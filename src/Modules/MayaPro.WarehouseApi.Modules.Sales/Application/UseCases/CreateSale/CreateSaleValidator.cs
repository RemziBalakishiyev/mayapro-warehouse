using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// Creating a sale carries no rules of its own — every check is the shared
/// <see cref="SaleWriteValidator{TCommand}"/> set, which <c>UpdateSaleValidator</c> applies too.
/// </summary>
public sealed class CreateSaleValidator : SaleWriteValidator<CreateSaleCommand>
{
}
