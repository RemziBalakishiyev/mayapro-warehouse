using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// What a salary entry does to the employee's account. A <see cref="Payment"/> is money actually handed
/// over (salary or advance) — it leaves the cash drawer, so day-end and the dashboard count it. A
/// <see cref="Deduction"/> (meals, transport, a fine) is only charged against the account; no cash moves.
/// Persisted by name; the wire contract uses <see cref="WireFormat.SalaryEntryTypes"/>.
/// </summary>
public enum SalaryEntryType
{
    Payment = 1,
    Deduction = 2
}

/// <summary>
/// Maps <see cref="SalaryEntryType"/> to/from the frontend codes. The code values live in
/// <see cref="WireFormat"/> (single source of truth).
/// </summary>
public static class SalaryEntryTypeCode
{
    public const string Payment = WireFormat.SalaryEntryTypes.Payment;
    public const string Deduction = WireFormat.SalaryEntryTypes.Deduction;

    public static string ToCode(this SalaryEntryType type) => type switch
    {
        SalaryEntryType.Payment => Payment,
        SalaryEntryType.Deduction => Deduction,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Naməlum maaş əməliyyatı növü")
    };

    public static bool TryParse(string? code, out SalaryEntryType type)
    {
        switch (code)
        {
            case Payment: type = SalaryEntryType.Payment; return true;
            case Deduction: type = SalaryEntryType.Deduction; return true;
            default: type = default; return false;
        }
    }
}
