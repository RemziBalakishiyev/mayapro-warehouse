namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>A customer as returned by the API.</summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Note,
    decimal Debt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
