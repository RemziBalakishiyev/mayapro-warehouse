using MayaPro.WarehouseApi.Modules.Expenses.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>Tests the expense category ↔ frontend-code mapping.</summary>
public sealed class ExpenseCategoryTests
{
    [Theory]
    [InlineData(ExpenseCategory.Transport, "Yol")]
    [InlineData(ExpenseCategory.Labor, "Fəhlə")]
    [InlineData(ExpenseCategory.Storage, "Anbar/Yer")]
    [InlineData(ExpenseCategory.Packaging, "Paket/Qutu")]
    [InlineData(ExpenseCategory.Store, "Mağaza")]
    [InlineData(ExpenseCategory.Other, "Digər")]
    public void ToCode_And_TryParse_Round_Trip(ExpenseCategory category, string code)
    {
        Assert.Equal(code, category.ToCode());

        Assert.True(ExpenseCategoryCode.TryParse(code, out ExpenseCategory parsed));
        Assert.Equal(category, parsed);
    }

    [Fact]
    public void TryParse_Rejects_Unknown_Code()
    {
        Assert.False(ExpenseCategoryCode.TryParse("Naməlum", out _));
    }
}
