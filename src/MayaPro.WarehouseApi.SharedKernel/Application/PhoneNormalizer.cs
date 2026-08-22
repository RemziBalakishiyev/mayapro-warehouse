namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// BE#46 — the single place a phone number becomes <b>canonical</b>. Canonical means twelve ASCII digits
/// starting with the country code: <c>994501234567</c>. No plus sign, no spaces, no dashes, no parentheses.
/// <para>
/// Everything that writes a phone (registration, login lookup, customers, suppliers, settings, tenants) runs
/// its input through here first, so the value stored in one module is byte-for-byte the value another module
/// compares against. Before this existed, <c>0501234567</c> and <c>+994 50 123 45 67</c> were two different
/// users to the database while being the same human being — which is exactly how a phone-based login and a
/// tenant-scoped unique index quietly go wrong.
/// </para>
/// <para>
/// <b>The three accepted shapes</b> (after every non-digit is dropped):
/// </para>
/// <list type="bullet">
///   <item>ten digits starting with <c>0</c> — the local form: the leading zero is replaced by <c>994</c>;</item>
///   <item>twelve digits starting with <c>994</c> — already canonical, returned unchanged;</item>
///   <item>anything else — refused. Note that a bare nine-digit <c>501234567</c> is <b>deliberately</b> a
///   failure: accepting it would mean guessing at a number the caller never actually typed.</item>
/// </list>
/// <para>
/// Nothing here throws. Failures come back as a <see cref="Result"/> carrying
/// <see cref="Error.Validation(string)"/>, whose <c>General.Validation</c> code the shared
/// <c>ResultExtensions</c> maps to HTTP 400 — the same path every other business rule takes.
/// </para>
/// <para>
/// The T-SQL in each module's <c>NormalizePhoneNumbers</c> migration implements this exact table. If one side
/// is ever changed the other must change with it, or rows written before the change stop matching the rows
/// written after it.
/// </para>
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// The literal refusal shown to the user. Frozen text — tests and the API docs quote it verbatim, and it
    /// names a valid example rather than describing the rule, because the person reading it is typing a phone
    /// number, not a specification.
    /// </summary>
    public const string InvalidFormatMessage = "Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)";

    /// <summary>
    /// Reused word-for-word from the existing FluentValidation rules (<c>LoginValidator</c>,
    /// <c>RegisterTenantValidator</c>) so a missing phone reads the same however it was caught.
    /// </summary>
    public const string EmptyMessage = "Telefon boş ola bilməz";

    /// <summary>Digits in a canonical number: <c>994</c> + a nine-digit subscriber number.</summary>
    private const int CanonicalLength = 12;

    /// <summary>Digits in the local form, e.g. <c>0501234567</c>.</summary>
    private const int LocalLength = 10;

    private const string CountryCode = "994";

    /// <summary>
    /// Upper bound on the raw input <see cref="TryCanonicalize"/> will even look at. No legitimate phone —
    /// however generously formatted, e.g. <c>"+994 (50) 123-45-67"</c> at 19 characters — comes anywhere near
    /// this. It exists because none of the callers (some validators have no <c>MaximumLength</c> rule on
    /// <c>Phone</c> at all) can be trusted to have already bounded the string, and <see cref="OnlyDigits"/>
    /// stack-allocates a buffer sized to the input: an unbounded, attacker-supplied <c>phone</c> field would
    /// otherwise turn into an unbounded <c>stackalloc</c> and a process-crashing <see cref="StackOverflowException"/>.
    /// Anything longer than this is refused as a format error before that buffer is ever touched.
    /// </summary>
    private const int MaxRawLength = 64;

    /// <summary>
    /// Normalizes a <b>required</b> phone. Empty input is a failure with <see cref="EmptyMessage"/>;
    /// unparsable input is a failure with <see cref="InvalidFormatMessage"/>.
    /// </summary>
    public static Result<string> Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<string>(Error.Validation(EmptyMessage));

        string? canonical = TryCanonicalize(raw);

        return canonical is null
            ? Result.Failure<string>(Error.Validation(InvalidFormatMessage))
            : Result.Success(canonical);
    }

    /// <summary>
    /// Normalizes an <b>optional</b> phone. Null/blank is a success carrying <c>null</c> — so the column is
    /// written as a genuine SQL <c>NULL</c> rather than an empty string, and "no phone" stays one value
    /// instead of two. Optional does <i>not</i> mean lenient: a value that is present but unparsable still
    /// fails with <see cref="InvalidFormatMessage"/>.
    /// </summary>
    public static Result<string?> NormalizeOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Success<string?>(null);

        string? canonical = TryCanonicalize(raw);

        return canonical is null
            ? Result.Failure<string?>(Error.Validation(InvalidFormatMessage))
            : Result.Success<string?>(canonical);
    }

    /// <summary>
    /// The rule itself: strip to digits, then match one of the two accepted lengths. Returns <c>null</c> when
    /// the input matches neither — the callers above turn that into the user-facing error, so the rule and its
    /// presentation stay separate.
    /// </summary>
    private static string? TryCanonicalize(string raw)
    {
        if (raw.Length > MaxRawLength)
            return null;

        string digits = OnlyDigits(raw);

        return digits.Length switch
        {
            LocalLength when digits[0] == '0' => CountryCode + digits[1..],
            CanonicalLength when digits.StartsWith(CountryCode, StringComparison.Ordinal) => digits,
            _ => null
        };
    }

    /// <summary>
    /// Keeps ASCII digits only. Deliberately not <see cref="char.IsDigit(char)"/>: that also accepts Arabic-
    /// Indic and other Unicode digit forms, which would produce a "canonical" string that no SQL comparison
    /// or <c>wa.me</c> link would ever match.
    /// </summary>
    private static string OnlyDigits(string raw)
    {
        Span<char> buffer = stackalloc char[raw.Length];
        int length = 0;

        foreach (char character in raw)
        {
            if (character is >= '0' and <= '9')
                buffer[length++] = character;
        }

        return new string(buffer[..length]);
    }
}
