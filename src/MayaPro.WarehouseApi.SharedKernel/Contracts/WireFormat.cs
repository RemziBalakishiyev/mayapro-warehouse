namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// The frozen wire vocabulary shared with the frontend. These Azerbaijani strings are the API contract —
/// the frontend sends and reads them verbatim (payment types, expense categories, roles), so they must
/// never change even though the C# identifiers around them are English. Every enum↔wire converter and
/// every wire-facing DTO resolves to these constants, so the contract lives in exactly one place.
/// </summary>
public static class WireFormat
{
    /// <summary>Sale payment types as the frontend sends them in <c>paymentType</c>.</summary>
    public static class PaymentTypes
    {
        public const string Cash = "Nağd";
        public const string Card = "Kart";
        public const string Credit = "Nisyə";
    }

    /// <summary>
    /// Expense source as the frontend sends/reads it in <c>source</c>: <c>"product"</c> raises that
    /// product's real cost, <c>"general"</c> is a store expense with no product effect.
    /// </summary>
    public static class ExpenseSources
    {
        public const string General = "general";
        public const string Product = "product";
    }

    /// <summary>User roles as the frontend reads them in the <c>role</c> field.</summary>
    public static class Roles
    {
        public const string Owner = "sahib";
        public const string Manager = "menecer";
        public const string Seller = "satici";
    }
}
