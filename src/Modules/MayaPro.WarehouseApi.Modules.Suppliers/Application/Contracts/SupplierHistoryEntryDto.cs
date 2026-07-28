namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;

/// <summary>
/// One row of a supplier's full debt history, in chronological order. <see cref="Type"/> is
/// <c>initialDebt</c> (opening balance) or <c>payment</c> (a payment that lowered what we owe them).
/// <see cref="Note"/> carries the free-text note for the opening balance / a payment.
/// </summary>
public sealed record SupplierHistoryEntryDto(DateTime Date, string Type, decimal Amount, string? Note);

/// <summary>The <see cref="SupplierHistoryEntryDto.Type"/> discriminator values.</summary>
public static class SupplierHistoryEntryType
{
    public const string InitialDebt = "initialDebt";
    public const string Payment = "payment";
}
