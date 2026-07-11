using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Tests the expense category ↔ frontend-code mapping and the product cost bucket each category feeds
/// (the <c>categoryToExpenseKey</c> rule, incl. Mağaza → Digər).
/// </summary>
public sealed class ExpenseCategoryTests
{
    [Theory]
    [InlineData(ExpenseCategory.Yol, "Yol")]
    [InlineData(ExpenseCategory.Fehle, "Fəhlə")]
    [InlineData(ExpenseCategory.AnbarYer, "Anbar/Yer")]
    [InlineData(ExpenseCategory.PaketQutu, "Paket/Qutu")]
    [InlineData(ExpenseCategory.Magaza, "Mağaza")]
    [InlineData(ExpenseCategory.Diger, "Digər")]
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

    [Theory]
    [InlineData(ExpenseCategory.Yol, ProductCostBucket.Yol)]
    [InlineData(ExpenseCategory.Fehle, ProductCostBucket.Fehle)]
    [InlineData(ExpenseCategory.AnbarYer, ProductCostBucket.Yer)]
    [InlineData(ExpenseCategory.PaketQutu, ProductCostBucket.Paket)]
    [InlineData(ExpenseCategory.Magaza, ProductCostBucket.Diger)] // no store bucket → Digər
    [InlineData(ExpenseCategory.Diger, ProductCostBucket.Diger)]
    public void ToCostBucket_Maps_Category_To_Product_Bucket(ExpenseCategory category, ProductCostBucket bucket)
    {
        Assert.Equal(bucket, category.ToCostBucket());
    }
}
