namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.CreateSupplier;

/// <summary>Input for creating a supplier. Optional opening <see cref="Debt"/> defaults to zero.</summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? ContactName,
    string? Phone,
    string? Note,
    decimal Debt = 0);
