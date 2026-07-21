using MayaPro.WarehouseApi.Modules.Customers.Domain;

namespace MayaPro.WarehouseApi.Modules.Customers.Tests;

/// <summary>Domain unit tests for <see cref="Customer"/> — the debt rules.</summary>
public sealed class CustomerTests
{
    [Fact]
    public void IncreaseDebt_Adds_To_Balance()
    {
        Customer customer = Customer.Create("Müştəri", debt: 100);

        customer.IncreaseDebt(50);

        Assert.Equal(150m, customer.Debt);
    }

    [Fact]
    public void DecreaseDebt_Up_To_Full_Balance_Succeeds()
    {
        Customer customer = Customer.Create("Müştəri", debt: 100);

        var result = customer.DecreaseDebt(100);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, customer.Debt);
    }

    [Fact]
    public void DecreaseDebt_Beyond_Balance_Fails_And_Leaves_Debt_Untouched()
    {
        Customer customer = Customer.Create("Müştəri", debt: 100);

        var result = customer.DecreaseDebt(150);

        Assert.True(result.IsFailure);
        Assert.Equal(CustomerErrors.PaymentExceedsDebt, result.Error);
        Assert.Equal(100m, customer.Debt);
    }

    [Fact]
    public void ReverseDebt_Unwinds_A_Credit_Sale_By_Its_Net_Amount()
    {
        // A deleted credit sale returns its net to the customer's balance (the inverse of the sale).
        Customer customer = Customer.Create("Müştəri", debt: 100);

        customer.ReverseDebt(35);

        Assert.Equal(65m, customer.Debt);
    }

    [Fact]
    public void ReverseDebt_Floors_At_Zero_When_The_Debt_Was_Already_Paid_Down()
    {
        // The sale's debt was since paid off; reversing it must never push the customer into credit.
        Customer customer = Customer.Create("Müştəri", debt: 10);

        customer.ReverseDebt(35);

        Assert.Equal(0m, customer.Debt);
    }

    [Fact]
    public void Update_Changes_Details_But_Not_Debt()
    {
        Customer customer = Customer.Create("Köhnə ad", phone: "050", note: "köhnə", debt: 100);

        customer.Update("Yeni ad", "055", "yeni qeyd");

        Assert.Equal("Yeni ad", customer.Name);
        Assert.Equal("055", customer.Phone);
        Assert.Equal("yeni qeyd", customer.Note);
        Assert.Equal(100m, customer.Debt); // untouched
    }
}
