namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierDebt;

/// <summary>A purchase on credit: increases what we owe the supplier. <see cref="SupplierId"/> from the route.</summary>
public sealed record AddSupplierDebtCommand(Guid SupplierId, decimal Amount, string? Note);
