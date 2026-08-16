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

    /// <summary>
    /// Salary entry kind as the frontend sends/reads it in <c>type</c>: <c>"payment"</c> is money actually
    /// leaving the drawer (salary/advance), <c>"deduction"</c> is only charged against the employee's
    /// account (meals, transport, fines) and never touches the cash.
    /// </summary>
    public static class SalaryEntryTypes
    {
        public const string Payment = "payment";
        public const string Deduction = "deduction";
    }

    /// <summary>
    /// Error codes the frontend <b>branches on</b> rather than merely displays, so the string itself is
    /// part of the contract and lives here with the rest of the frozen vocabulary.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// BE#36/BE#41 — the subscription gate's 403. Deliberately outside the project's
        /// <c>&lt;Modul&gt;.&lt;Səbəb&gt;&lt;Status&gt;</c> naming convention: this is an
        /// infrastructure-level middleware refusal that belongs to no module, and the value is frozen by
        /// the specification because the frontend hard-codes it to show the "renew your subscription"
        /// screen. Its 403 is mapped by name in <c>ResultExtensions.StatusCodeFor</c>, since the usual
        /// <c>...Forbidden</c> suffix rule cannot apply to it.
        /// </summary>
        public const string SubscriptionExpired = "SubscriptionExpired";
    }

    /// <summary>User roles as the frontend reads them in the <c>role</c> field.</summary>
    public static class Roles
    {
        public const string Owner = "sahib";
        public const string Manager = "menecer";
        public const string Seller = "satici";

        /// <summary>
        /// BE#36 — the platform operator. Added, never substituted: the three shop roles above keep their
        /// frozen values. This one belongs to no shop, so the shop UI never renders it; only the platform
        /// admin surface (<c>/api/admin/*</c>) does.
        /// </summary>
        public const string PlatformAdmin = "platform_admin";
    }
}
