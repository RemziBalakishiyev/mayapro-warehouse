using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Domain;

/// <summary>
/// How a sale was paid. Persisted by name (Cash/Card/Credit); the wire contract uses the frontend codes
/// (<c>"Nağd" | "Kart" | "Nisyə"</c>) — see <see cref="PaymentTypeCode"/>.
/// </summary>
public enum PaymentType
{
    Cash = 1,
    Card = 2,
    Credit = 3
}

/// <summary>
/// Maps <see cref="PaymentType"/> to/from the frontend payment codes, which are the API contract for the
/// <c>paymentType</c> field. The code values live in <see cref="WireFormat"/> (single source of truth).
/// </summary>
public static class PaymentTypeCode
{
    public const string Cash = WireFormat.PaymentTypes.Cash;
    public const string Card = WireFormat.PaymentTypes.Card;
    public const string Credit = WireFormat.PaymentTypes.Credit;

    public static string ToCode(this PaymentType type) => type switch
    {
        PaymentType.Cash => Cash,
        PaymentType.Card => Card,
        PaymentType.Credit => Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Naməlum ödəniş növü")
    };

    public static bool TryParse(string? code, out PaymentType type)
    {
        switch (code)
        {
            case Cash:
                type = PaymentType.Cash;
                return true;
            case Card:
                type = PaymentType.Card;
                return true;
            case Credit:
                type = PaymentType.Credit;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
