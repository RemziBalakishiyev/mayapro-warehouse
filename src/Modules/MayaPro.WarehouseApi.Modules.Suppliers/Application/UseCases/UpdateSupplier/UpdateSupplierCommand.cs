namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.UpdateSupplier;

/// <summary>
/// Edits a supplier's details. <see cref="Id"/> comes from the route. Debt is never changed here — it only
/// moves through purchases (debts) and payments.
/// </summary>
public sealed record UpdateSupplierCommand(Guid Id, string Name, string? ContactName, string? Phone, string? Note);
