namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;

/// <summary>
/// Body of <c>POST /api/exports/products/labels.pdf</c>: the products (and how many copies of each) to
/// print, plus the code style shared by every label in the sheet.
/// </summary>
public sealed record LabelsPdfRequest(IReadOnlyList<LabelItemRequest>? Items, string? Type);

/// <summary>One product and how many identical labels to print for it, in the order they should appear.</summary>
public sealed record LabelItemRequest(Guid ProductId, int Count);
