using System.Globalization;
using System.Reflection;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportProductLabelsPdf;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// QA coverage for BE#12 acceptance criterion AC6 (the printed sheet's physical spec: A4, 3×8 grid,
/// ~63×34mm labels, bold "12.50 ₼" price, truncated product name) that the feature's own test suite did
/// not directly assert. <see cref="ExportProductLabelsPdfHandler"/> keeps the grid geometry and the price/
/// name formatting as private constants/methods — there is no public seam to observe them from a
/// black-box HTTP or handler test, so this file reaches them via reflection. That is deliberate: pinning
/// these exact numbers/format down here means a future refactor that silently changes the sticker-sheet
/// layout (e.g. drops to 2 columns, or lets the price render with a locale-dependent decimal separator)
/// fails a test instead of only being caught by eye on a printed page.
/// </summary>
public sealed class LabelSheetLayoutSpecTests
{
    private static readonly Type HandlerType = typeof(ExportProductLabelsPdfHandler);

    [Fact]
    public void Grid_Is_Three_Columns_By_Eight_Rows_Of_63x34mm_Labels_With_A_Cut_Gap()
    {
        Assert.Equal(3, ConstValue<int>("Columns"));
        Assert.Equal(8, ConstValue<int>("Rows"));
        Assert.Equal(63f, ConstValue<float>("LabelWidthMm"));
        Assert.Equal(34f, ConstValue<float>("LabelHeightMm"));
        Assert.True(ConstValue<float>("GapMm") > 0f, "A zero cut gap would make adjacent labels impossible to trim apart.");
    }

    /// <summary>AC5: a single sheet may carry at most 500 labels in total.</summary>
    [Fact]
    public void Sheet_Wide_Label_Cap_Is_500()
    {
        Assert.Equal(500, ConstValue<int>("MaxLabels"));
    }

    [Theory]
    [InlineData(12.5, "12.50 ₼")]
    [InlineData(0, "0.00 ₼")]
    [InlineData(999.995, "1,000.00 ₼")] // AwayFromZero-style .NET "N2" rounding at the half-cent boundary, thousands separator included
    [InlineData(7, "7.00 ₼")]
    public void Price_Renders_As_Two_Decimals_Followed_By_The_Manat_Sign(decimal salePrice, string expected)
    {
        string formatted = InvokeFormatPrice(salePrice);

        Assert.Equal(expected, formatted);
    }

    /// <summary>
    /// A sticker printed on a machine whose regional settings use a comma decimal separator (very common
    /// for az/ru/de locales) must still read "12.50 ₼", not "12,50 ₼" — the doc comment on FormatPrice
    /// promises exactly this, so it is worth locking in with the current thread culture actually swapped.
    /// </summary>
    [Fact]
    public void Price_Format_Does_Not_Follow_The_Servers_Current_Culture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); // comma decimal separator
            string formatted = InvokeFormatPrice(12.5m);

            Assert.Equal("12.50 ₼", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Short_Product_Name_Is_Printed_Unchanged()
    {
        string result = InvokeTruncateName("Ağ köynək");

        Assert.Equal("Ağ köynək", result);
    }

    /// <summary>
    /// A name long enough to overflow the ~two-line label area is cut with a trailing ellipsis rather than
    /// left to overflow the sticker — the layout-level ClampLines(2) is a second safety net, not the only
    /// one.
    /// </summary>
    [Fact]
    public void Long_Product_Name_Is_Truncated_With_An_Ellipsis()
    {
        string longName = new string('A', 60);

        string result = InvokeTruncateName(longName);

        Assert.True(result.Length <= 40, $"Truncated name should be at most 40 characters, was {result.Length}");
        Assert.EndsWith("...", result);
        Assert.StartsWith(new string('A', 37), result);
    }

    [Fact]
    public void Name_At_Exactly_The_Cap_Is_Not_Truncated()
    {
        string exactlyFortyChars = new string('B', 40);

        string result = InvokeTruncateName(exactlyFortyChars);

        Assert.Equal(exactlyFortyChars, result);
    }

    private static T ConstValue<T>(string fieldName)
    {
        FieldInfo? field = HandlerType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(field is not null, $"Expected a private const field named '{fieldName}' on {HandlerType.Name}.");
        return (T)field!.GetValue(null)!;
    }

    private static string InvokeFormatPrice(decimal salePrice)
    {
        MethodInfo? method = HandlerType.GetMethod(
            "FormatPrice", BindingFlags.NonPublic | BindingFlags.Static, [typeof(decimal)]);
        Assert.True(method is not null, "Expected a private static FormatPrice(decimal) method.");
        return (string)method!.Invoke(null, [salePrice])!;
    }

    private static string InvokeTruncateName(string name)
    {
        MethodInfo? method = HandlerType.GetMethod(
            "TruncateName", BindingFlags.NonPublic | BindingFlags.Static, [typeof(string)]);
        Assert.True(method is not null, "Expected a private static TruncateName(string) method.");
        return (string)method!.Invoke(null, [name])!;
    }
}
