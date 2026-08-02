using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// The salary entry type ↔ frontend-code mapping ("payment" | "deduction") and the <c>yyyy-MM</c> month
/// format — both are frozen wire vocabulary (ADR-0006), so they are pinned here rather than assumed.
/// </summary>
public sealed class SalaryWireFormatTests
{
    [Theory]
    [InlineData(SalaryEntryType.Payment, "payment")]
    [InlineData(SalaryEntryType.Deduction, "deduction")]
    public void ToCode_And_TryParse_Round_Trip(SalaryEntryType type, string code)
    {
        Assert.Equal(code, type.ToCode());

        Assert.True(SalaryEntryTypeCode.TryParse(code, out SalaryEntryType parsed));
        Assert.Equal(type, parsed);
    }

    [Fact]
    public void Codes_Come_From_The_Frozen_Wire_Vocabulary()
    {
        Assert.Equal(WireFormat.SalaryEntryTypes.Payment, SalaryEntryTypeCode.Payment);
        Assert.Equal(WireFormat.SalaryEntryTypes.Deduction, SalaryEntryTypeCode.Deduction);
    }

    [Theory]
    [InlineData("bonus")]
    [InlineData("Payment")]
    [InlineData(null)]
    public void TryParse_Rejects_Anything_Else(string? code)
    {
        Assert.False(SalaryEntryTypeCode.TryParse(code, out _));
    }

    [Fact]
    public void Month_Is_Formatted_As_Yyyy_Mm_Regardless_Of_Culture()
    {
        Assert.Equal("2026-03", SalaryMonth.From(new DateOnly(2026, 3, 15)));
        Assert.Equal("2026-12", SalaryMonth.From(new DateOnly(2026, 12, 31)));
    }

    [Theory]
    [InlineData("2026-13")]   // month 13 does not exist
    [InlineData("26-8")]      // wrong widths
    [InlineData("2026-8")]    // wrong widths
    [InlineData("avqust")]
    [InlineData("2026-03-01")]
    [InlineData("")]
    [InlineData(null)]
    public void Malformed_Months_Are_Rejected(string? month)
    {
        Assert.False(SalaryMonth.TryParse(month, out _));
    }

    [Fact]
    public void Valid_Month_Is_Normalised()
    {
        Assert.True(SalaryMonth.TryParse("2026-03", out string normalized));
        Assert.Equal("2026-03", normalized);
    }
}
