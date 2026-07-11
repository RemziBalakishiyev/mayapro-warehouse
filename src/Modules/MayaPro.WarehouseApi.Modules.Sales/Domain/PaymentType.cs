namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// How a sale was paid. Persisted by name (Nagd/Kart/Nisye); the wire contract uses the frontend codes
/// (<c>"Nağd" | "Kart" | "Nisyə"</c>) — see <see cref="PaymentTypeCode"/>.
/// </summary>
public enum PaymentType
{
    Nagd = 1,
    Kart = 2,
    Nisye = 3
}

/// <summary>
/// Maps <see cref="PaymentType"/> to/from the frontend payment codes, which are the API contract for the
/// <c>paymentType</c> field.
/// </summary>
public static class PaymentTypeCode
{
    public const string Nagd = "Nağd";
    public const string Kart = "Kart";
    public const string Nisye = "Nisyə";

    public static string ToCode(this PaymentType type) => type switch
    {
        PaymentType.Nagd => Nagd,
        PaymentType.Kart => Kart,
        PaymentType.Nisye => Nisye,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Naməlum ödəniş növü")
    };

    public static bool TryParse(string? code, out PaymentType type)
    {
        switch (code)
        {
            case Nagd:
                type = PaymentType.Nagd;
                return true;
            case Kart:
                type = PaymentType.Kart;
                return true;
            case Nisye:
                type = PaymentType.Nisye;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
