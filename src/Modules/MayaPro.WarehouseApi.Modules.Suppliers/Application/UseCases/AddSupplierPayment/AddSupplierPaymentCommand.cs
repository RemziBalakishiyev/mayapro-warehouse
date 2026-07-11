namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.AddSupplierPayment;

/// <summary>A payment we make against a supplier's debt. <see cref="SupplierId"/> comes from the route.</summary>
public sealed record AddSupplierPaymentCommand(Guid SupplierId, decimal Amount, string? Note);
