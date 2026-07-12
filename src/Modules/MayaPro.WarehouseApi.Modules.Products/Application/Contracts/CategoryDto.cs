namespace MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

/// <summary>A managed category as returned by <c>GET /api/categories</c>.</summary>
public sealed record CategoryDto(Guid Id, string Name);
