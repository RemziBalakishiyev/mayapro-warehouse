using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateProduct;

namespace MayaPro.WarehouseApi.Modules.Products.Tests;

/// <summary>
/// Validation of the new dynamic product attributes: names may not be blank, and a product may carry at
/// most 15 attributes.
/// </summary>
public sealed class ProductAttributeValidationTests
{
    private readonly CreateProductValidator _validator = new();

    [Fact]
    public async Task Valid_Attributes_Pass()
    {
        CreateProductCommand command = NewCommand(new ProductAttributeDto("Ölçü", "M"), new ProductAttributeDto("Rəng", "Qara"));

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Empty_Attributes_List_Passes()
    {
        CreateProductCommand command = NewCommand();

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_Attribute_Name_Fails(string name)
    {
        CreateProductCommand command = NewCommand(new ProductAttributeDto(name, "dəyər"));

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Xüsusiyyət adı boş ola bilməz");
    }

    [Fact]
    public async Task More_Than_Fifteen_Attributes_Fails()
    {
        ProductAttributeDto[] attributes = Enumerable
            .Range(1, 16)
            .Select(i => new ProductAttributeDto($"Ad{i}", $"Dəyər{i}"))
            .ToArray();
        CreateProductCommand command = NewCommand(attributes);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ən çoxu 15 xüsusiyyət əlavə etmək olar");
    }

    [Fact]
    public async Task Exactly_Fifteen_Attributes_Passes()
    {
        ProductAttributeDto[] attributes = Enumerable
            .Range(1, 15)
            .Select(i => new ProductAttributeDto($"Ad{i}", $"Dəyər{i}"))
            .ToArray();
        CreateProductCommand command = NewCommand(attributes);

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    private static CreateProductCommand NewCommand(params ProductAttributeDto[] attributes) =>
        new(
            Name: "Test məhsul",
            Category: "Test",
            Attributes: attributes,
            Barcode: "TST-VAL",
            Image: "",
            Note: "",
            PurchasePrice: 5m,
            SalePrice: 10m,
            Quantity: 10,
            MinStock: 1,
            Currency: "AZN",
            SupplierId: "sup_1",
            Location: "Anbar A / Rəf 1 / Qutu 1",
            Store: "Anbar A",
            Warehouse: "Anbar A",
            Shelf: "1",
            Box: "1",
            Expenses: Array.Empty<ProductExpenseItemDto>());
}
