namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CommitProductsImport;

/// <summary>Input for <c>POST /api/imports/products/commit</c> — the token a prior preview returned.</summary>
public sealed record CommitProductsImportCommand(string? ImportToken);
