using MayaPro.WarehouseApi.Modules.Expenses.Domain;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>Tests the expense source ↔ frontend-code mapping ("general" | "product").</summary>
public sealed class ExpenseSourceCodeTests
{
    [Theory]
    [InlineData(ExpenseSource.General, "general")]
    [InlineData(ExpenseSource.Product, "product")]
    public void ToCode_And_TryParse_Round_Trip(ExpenseSource source, string code)
    {
        Assert.Equal(code, source.ToCode());

        Assert.True(ExpenseSourceCode.TryParse(code, out ExpenseSource parsed));
        Assert.Equal(source, parsed);
    }

    [Fact]
    public void TryParse_Rejects_Unknown_Code()
    {
        Assert.False(ExpenseSourceCode.TryParse("unknown", out _));
    }
}
