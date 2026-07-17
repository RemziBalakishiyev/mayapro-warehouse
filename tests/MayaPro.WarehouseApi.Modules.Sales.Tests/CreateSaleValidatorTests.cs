using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>Validation rules for <see cref="CreateSaleCommand"/>.</summary>
public sealed class CreateSaleValidatorTests
{
    private static readonly CreateSaleValidator Validator = new();

    [Fact]
    public void Credit_Sale_Without_Customer_Is_Invalid()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 10m,
            Discount: 0m,
            PaymentType: "Nisyə",
            CustomerId: null,
            Note: null);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Nisyə satış üçün müştəri seçilməlidir");
    }

    [Fact]
    public void Discount_Greater_Than_Subtotal_Is_Invalid()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 2,
            SalePrice: 10m,     // subtotal = 20
            Discount: 25m,      // > subtotal
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Endirim satış məbləğindən çox ola bilməz");
    }

    [Fact]
    public void Valid_Cash_Sale_Passes()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 2,
            SalePrice: 10m,
            Discount: 5m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Manual_Sale_Without_ProductName_Is_Invalid()
    {
        // ProductId null → free-form sale, so the name is mandatory (nothing else supplies it).
        var command = new CreateSaleCommand(
            ProductId: null,
            Quantity: 1,
            SalePrice: 10m,
            Discount: 0m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "   ");   // blank → not a real name

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Sərbəst satışda mal adı məcburidir");
    }

    [Fact]
    public void Valid_Manual_Sale_With_Name_Passes()
    {
        var command = new CreateSaleCommand(
            ProductId: null,
            Quantity: 2,
            SalePrice: 15m,
            Discount: 0m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "Əl ilə mal",
            CostPerUnit: null);   // cost optional — unknown is allowed

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
