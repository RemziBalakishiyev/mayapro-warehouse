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
            PaymentType: "Nisyə",
            CustomerId: null,
            Note: null);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Nisyə satış üçün müştəri seçilməlidir");
    }

    [Fact]
    public void Valid_Cash_Sale_Passes()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 2,
            SalePrice: 10m,
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
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "Əl ilə mal",
            CostPerUnit: null);   // cost optional — unknown is allowed

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Negative_PurchasePricePerUnit_Is_Invalid()
    {
        // TC-10: a negative purchase price is rejected, not silently accepted.
        var command = new CreateSaleCommand(
            ProductId: null,
            Quantity: 1,
            SalePrice: 10m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "Əl ilə mal",
            PurchasePricePerUnit: -10m);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Alış qiyməti mənfi ola bilməz");
    }

    [Fact]
    public void Null_PurchasePricePerUnit_Is_Valid()
    {
        // AC-8: not supplying the purchase price is fine — no exception, no validation error.
        var command = new CreateSaleCommand(
            ProductId: null,
            Quantity: 1,
            SalePrice: 10m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "Əl ilə mal",
            PurchasePricePerUnit: null);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Zero_PurchasePricePerUnit_Is_Valid()
    {
        var command = new CreateSaleCommand(
            ProductId: null,
            Quantity: 1,
            SalePrice: 10m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            ProductName: "Əl ilə mal",
            PurchasePricePerUnit: 0m);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
