using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.SharedKernel.Tests;

/// <summary>
/// BE#46, AC-2/AC-3/AC-4 — the canonical phone rule, pinned. Every module writes phones through this type, so
/// the table below is the contract the migrations' T-SQL must reproduce exactly: change one without the other
/// and rows written before the change stop matching rows written after it.
/// <para>
/// The refusals matter as much as the successes. A bare nine-digit number is rejected on purpose (TC-6): the
/// alternative is guessing at a leading zero the caller never typed.
/// </para>
/// </summary>
public sealed class PhoneNormalizerTests
{
    // TC-1..TC-4, TC-14 — everything that is really the same number, however it was typed.
    [Theory]
    [InlineData("050 123-45-67")]
    [InlineData("050 123 45 67")]
    [InlineData("050-123-45-67")]
    [InlineData("(050) 123 45 67")]
    [InlineData("+994 50 123 45 67")]
    [InlineData("+994 (50) 123-45-67")]
    [InlineData("0501234567")]
    [InlineData("994501234567")]
    [InlineData("+994501234567")]
    [InlineData("  050 123 45 67  ")]
    [InlineData("050.123.45.67")]
    public void Every_Accepted_Spelling_Becomes_The_Same_Canonical_Number(string raw)
    {
        Result<string> result = PhoneNormalizer.Normalize(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal("994501234567", result.Value);
    }

    // TC-5..TC-9 — the refusals, each with the one frozen message.
    [Theory]
    [InlineData("12345")]
    [InlineData("501234567")]        // nine digits: deliberately not guessed at
    [InlineData("00994501234567")]   // fourteen digits
    [InlineData("1234567890")]       // ten digits but no leading zero
    [InlineData("123456789012")]     // twelve digits but not the 994 country code
    [InlineData("abc")]
    [InlineData("---")]
    [InlineData("+994 50 123 45 6")] // eleven digits
    public void Anything_Outside_The_Two_Accepted_Shapes_Is_Refused(string raw)
    {
        Result<string> result = PhoneNormalizer.Normalize(raw);

        Assert.True(result.IsFailure);
        Assert.Equal("Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)", result.Error.Message);
        Assert.Equal("General.Validation", result.Error.Code);
    }

    // TC-10 — "no phone" is one value (null), never an empty string, so the column stays a real SQL NULL.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_Absent_Optional_Phone_Is_Allowed_And_Becomes_Null(string? raw)
    {
        Result<string?> result = PhoneNormalizer.NormalizeOptional(raw);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    // TC-11 — the required overload reuses the existing validator wording for a missing phone.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_Absent_Required_Phone_Reuses_The_Existing_Empty_Message(string? raw)
    {
        Result<string> result = PhoneNormalizer.Normalize(raw);

        Assert.True(result.IsFailure);
        Assert.Equal("Telefon boş ola bilməz", result.Error.Message);
    }

    // TC-13 — optional means "may be absent", not "may be wrong".
    [Fact]
    public void An_Optional_Phone_That_Is_Present_But_Unparsable_Still_Fails()
    {
        Result<string?> result = PhoneNormalizer.NormalizeOptional("12345");

        Assert.True(result.IsFailure);
        Assert.Equal("Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)", result.Error.Message);
    }

    [Fact]
    public void An_Optional_Phone_Normalizes_The_Same_Way_As_A_Required_One()
    {
        Result<string?> result = PhoneNormalizer.NormalizeOptional("+994 (50) 123-45-67");

        Assert.True(result.IsSuccess);
        Assert.Equal("994501234567", result.Value);
    }

    // TC-12 — idempotence is what lets the migration be re-run and the API re-save without drift.
    [Theory]
    [InlineData("050 123 45 67")]
    [InlineData("+994551112233")]
    [InlineData("0701234567")]
    [InlineData("994121234567")]
    public void Normalizing_An_Already_Canonical_Number_Changes_Nothing(string raw)
    {
        string once = PhoneNormalizer.Normalize(raw).Value;
        string twice = PhoneNormalizer.Normalize(once).Value;

        Assert.Equal(once, twice);
        Assert.Equal(12, once.Length);
        Assert.StartsWith("994", once, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("050 111 22 33", "994501112233")]
    [InlineData("(012) 555 44 33", "994125554433")]
    [InlineData("+994 55 111 22 33", "994551112233")]
    [InlineData("070-123-45-67", "994701234567")]
    [InlineData("0509999999", "994509999999")]
    public void The_Local_Form_Swaps_Its_Leading_Zero_For_The_Country_Code(string raw, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(raw).Value);
    }

    /// <summary>
    /// BE#46, AC-12/TC-42/TC-44 — the frontend's <c>waLink()</c> and <c>phoneDigits()</c> build a
    /// <c>wa.me</c> URL by stripping everything that is not a digit. This asserts, from the backend side, that
    /// the canonical value survives that step untouched: <c>https://wa.me/994501234567</c> is what those
    /// helpers produce, so no frontend change is needed and none was made.
    /// </summary>
    [Theory]
    [InlineData("050 123 45 67")]
    [InlineData("+994 (55) 111-22-33")]
    [InlineData("994121234567")]
    public void The_Canonical_Value_Survives_The_Frontends_Digits_Only_Cleanup(string raw)
    {
        string canonical = PhoneNormalizer.Normalize(raw).Value;

        // Exactly what src/features/customers/lib.ts does before building the link.
        string afterFrontendCleanup = new(canonical.Where(char.IsAsciiDigit).ToArray());

        Assert.Equal(canonical, afterFrontendCleanup);
        Assert.Equal($"https://wa.me/{canonical}", $"https://wa.me/{afterFrontendCleanup}");
    }

    /// <summary>
    /// Unicode digits are not ASCII digits. Accepting them would produce a string that looks canonical to a
    /// human and matches nothing in SQL or in a <c>wa.me</c> link.
    /// </summary>
    [Fact]
    public void Non_Ascii_Digit_Characters_Are_Not_Treated_As_Digits()
    {
        Result<string> result = PhoneNormalizer.Normalize("٠٥٠١٢٣٤٥٦٧");

        Assert.True(result.IsFailure);
        Assert.Equal("Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)", result.Error.Message);
    }
}
