using MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

namespace MayaPro.WarehouseApi.Modules.Sales.Tests;

/// <summary>Validation rules for <see cref="CreateSaleCommand"/>.</summary>
public sealed class CreateSaleValidatorTests
{
    private static readonly CreateSaleValidator Validator = new();

    [Fact]
    public void Credit_Sale_Without_Customer_Is_Invalid()
    {
        // BE#15: the message is now the balance-agnostic-of-payment-type one — a Nisyə sale with no paid
        // amount defaults to fully unpaid (remaining = total > 0), so the customer is still mandatory.
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 10m,
            PaymentType: "Nisyə",
            CustomerId: null,
            Note: null);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Qalıq borc üçün müştəri seçilməlidir");
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

    // ── Qismən ödənişli satış (BE#15) ───────────────────────────────────────────────────────────────

    [Fact]
    public void TC5_Zero_Paid_Cash_Sale_Without_Customer_Is_Invalid()
    {
        // Nağd requested but paidAmount explicitly 0 → the whole total remains owed, so a customer is
        // mandatory — regardless of the requested payment type.
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 150m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            PaidAmount: 0m);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Qalıq borc üçün müştəri seçilməlidir");
    }

    [Fact]
    public void TC6_PaidAmount_Above_Total_Is_Invalid()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 300m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            PaidAmount: 350m);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ödənilən məbləğ ümumi məbləğdən çox ola bilməz");
    }

    [Fact]
    public void TC7_Negative_PaidAmount_Is_Invalid()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 300m,
            PaymentType: "Nağd",
            CustomerId: null,
            Note: null,
            PaidAmount: -50m);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ödənilən məbləğ mənfi ola bilməz");
    }

    [Fact]
    public void Partial_Credit_Payment_With_Customer_Is_Valid()
    {
        // TC1 shape: Nisyə, partial paidAmount, customer supplied → no validation error.
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 500m,
            PaymentType: "Nisyə",
            CustomerId: Guid.NewGuid(),
            Note: null,
            PaidAmount: 300m,
            PaidVia: "Nağd");

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unrecognised_PaidVia_Code_Is_Invalid()
    {
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 500m,
            PaymentType: "Nisyə",
            CustomerId: Guid.NewGuid(),
            Note: null,
            PaidAmount: 300m,
            PaidVia: "Bitcoin");

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ödəniş üsulu Nağd və ya Kart olmalıdır");
    }

    [Fact]
    public void Fully_Paid_Credit_Request_Without_Customer_Is_Valid()
    {
        // Requested Nisyə but paidAmount equals the total → remaining is zero, so no customer is required.
        var command = new CreateSaleCommand(
            ProductId: Guid.NewGuid(),
            Quantity: 1,
            SalePrice: 400m,
            PaymentType: "Nisyə",
            CustomerId: null,
            Note: null,
            PaidAmount: 400m);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
