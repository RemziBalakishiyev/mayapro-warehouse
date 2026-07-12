namespace MayaPro.WarehouseApi.Modules.Products.Application.Contracts;

/// <summary>
/// A dynamic product attribute on the wire: <c>{ "name": "...", "value": "..." }</c> (camelCase by the
/// API's default JSON policy). Replaces the old fixed size/color/model fields.
/// </summary>
public sealed record ProductAttributeDto(string Name, string Value);
