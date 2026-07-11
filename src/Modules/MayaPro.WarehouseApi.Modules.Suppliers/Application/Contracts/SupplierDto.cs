namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>A supplier as returned by the API.</summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Note,
    decimal Debt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
