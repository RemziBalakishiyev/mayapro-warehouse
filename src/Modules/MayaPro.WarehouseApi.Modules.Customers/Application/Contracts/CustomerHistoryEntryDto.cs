namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>
/// One row of a customer's full debt history, in chronological order. <see cref="Type"/> is
/// <c>initialDebt</c> (opening balance), <c>sale</c> (a credit purchase that raised the debt) or
/// <c>payment</c> (a payment that lowered it). <see cref="Note"/> carries the product line for a sale and
/// the free-text note for the opening balance / a payment.
/// </summary>
public sealed record CustomerHistoryEntryDto(
    DateTime Date,
    string Type,
    decimal Amount,
    string? Note);

/// <summary>The <see cref="CustomerHistoryEntryDto.Type"/> discriminator values.</summary>
public static class CustomerHistoryEntryType
{
    public const string InitialDebt = "initialDebt";
    public const string Sale = "sale";
    public const string Payment = "payment";
}
