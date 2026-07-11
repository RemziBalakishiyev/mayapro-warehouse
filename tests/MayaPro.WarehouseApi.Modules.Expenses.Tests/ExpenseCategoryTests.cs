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

    [Theory]
    [InlineData(ExpenseCategory.Transport, ProductCostBucket.Transport)]
    [InlineData(ExpenseCategory.Labor, ProductCostBucket.Labor)]
    [InlineData(ExpenseCategory.Storage, ProductCostBucket.Storage)]
    [InlineData(ExpenseCategory.Packaging, ProductCostBucket.Packaging)]
    [InlineData(ExpenseCategory.Store, ProductCostBucket.Other)] // no store bucket → Digər
    [InlineData(ExpenseCategory.Other, ProductCostBucket.Other)]
    public void ToCostBucket_Maps_Category_To_Product_Bucket(ExpenseCategory category, ProductCostBucket bucket)
    {
        Assert.Equal(bucket, category.ToCostBucket());
    }
}
