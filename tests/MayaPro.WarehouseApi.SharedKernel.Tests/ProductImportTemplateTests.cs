using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// The import template is a file contract between two modules (Exports writes it, Products validates it) and
/// the store user's saved copy. These tests pin the parts that would silently break both sides: the column
/// numbers must keep pointing at the header they are named after, and the required columns must keep their
/// "*" marker.
/// </summary>
public sealed class ProductImportTemplateTests
{
    [Theory]
    [InlineData(ProductImportTemplate.NameColumn, ProductImportTemplate.NameHeader)]
    [InlineData(ProductImportTemplate.CategoryColumn, ProductImportTemplate.CategoryHeader)]
    [InlineData(ProductImportTemplate.BarcodeColumn, ProductImportTemplate.BarcodeHeader)]
    [InlineData(ProductImportTemplate.PurchasePriceColumn, ProductImportTemplate.PurchasePriceHeader)]
    [InlineData(ProductImportTemplate.SalePriceColumn, ProductImportTemplate.SalePriceHeader)]
    [InlineData(ProductImportTemplate.QuantityColumn, ProductImportTemplate.QuantityHeader)]
    [InlineData(ProductImportTemplate.MinStockColumn, ProductImportTemplate.MinStockHeader)]
    [InlineData(ProductImportTemplate.WarehouseColumn, ProductImportTemplate.WarehouseHeader)]
    [InlineData(ProductImportTemplate.StoreColumn, ProductImportTemplate.StoreHeader)]
    [InlineData(ProductImportTemplate.ShelfColumn, ProductImportTemplate.ShelfHeader)]
    [InlineData(ProductImportTemplate.BoxColumn, ProductImportTemplate.BoxHeader)]
    [InlineData(ProductImportTemplate.AttributesColumn, ProductImportTemplate.AttributesHeader)]
    [InlineData(ProductImportTemplate.NoteColumn, ProductImportTemplate.NoteHeader)]
    public void Every_Column_Number_Points_At_Its_Own_Header(int column, string header) =>
        Assert.Equal(header, ProductImportTemplate.Headers[column - 1]);

    [Fact]
    public void The_Header_List_Covers_Exactly_The_Declared_Columns() =>
        Assert.Equal(ProductImportTemplate.NoteColumn, ProductImportTemplate.Headers.Count);

    [Fact]
    public void Required_Columns_Are_The_Ones_Marked_With_A_Star()
    {
        string[] required = ProductImportTemplate.Headers.Where(h => h.EndsWith('*')).ToArray();

        Assert.Equal(
            [
                ProductImportTemplate.NameHeader,
                ProductImportTemplate.PurchasePriceHeader,
                ProductImportTemplate.SalePriceHeader,
                ProductImportTemplate.QuantityHeader
            ],
            required);
    }
}
