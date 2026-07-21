using MayaPro.WarehouseApi.Modules.Suppliers.Domain;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Tests;

/// <summary>Domain unit tests for <see cref="Supplier"/> — the debt rules.</summary>
public sealed class SupplierTests
{
    [Fact]
    public void IncreaseDebt_Adds_To_Balance()
    {
        Supplier supplier = Supplier.Create("Təchizatçı", debt: 100);

        supplier.IncreaseDebt(250);

        Assert.Equal(350m, supplier.Debt);
    }

    [Fact]
    public void DecreaseDebt_Up_To_Full_Balance_Succeeds()
    {
        Supplier supplier = Supplier.Create("Təchizatçı", debt: 100);

        var result = supplier.DecreaseDebt(100);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, supplier.Debt);
    }

    [Fact]
    public void DecreaseDebt_Beyond_Balance_Fails_And_Leaves_Debt_Untouched()
    {
        Supplier supplier = Supplier.Create("Təchizatçı", debt: 100);

        var result = supplier.DecreaseDebt(150);

        Assert.True(result.IsFailure);
        Assert.Equal(SupplierErrors.PaymentExceedsDebt, result.Error);
        Assert.Equal(100m, supplier.Debt);
    }

    [Fact]
    public void Update_Changes_Details_But_Not_Debt()
    {
        Supplier supplier = Supplier.Create("Köhnə ad", contactName: "Əli", phone: "050", note: "köhnə", debt: 100);

        supplier.Update("Yeni ad", "Vəli", "055", "yeni qeyd");

        Assert.Equal("Yeni ad", supplier.Name);
        Assert.Equal("Vəli", supplier.ContactName);
        Assert.Equal("055", supplier.Phone);
        Assert.Equal("yeni qeyd", supplier.Note);
        Assert.Equal(100m, supplier.Debt); // untouched
    }
}
