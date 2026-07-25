namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>
/// One row of a customer's full history, in chronological order. <see cref="Type"/> is
/// <c>initialDebt</c> (opening balance), <c>sale</c> (a purchase of ANY payment type) or <c>payment</c>
/// (a payment that lowered the debt). <see cref="PaymentType"/> is set only on <c>sale</c> rows (wire
/// code Nağd/Kart/Nisyə) — only Nisyə rows raised the debt; the frontend tells them apart by this field.
/// <see cref="Note"/> carries the product line for a sale and the free-text note for the opening
/// balance / a payment. <see cref="SaleId"/> is set only for <c>sale</c> rows.
/// </summary>
public sealed record CustomerHistoryEntryDto(
    DateTime Date,
    string Type,
    decimal Amount,
    string? Note,
    Guid? SaleId,
    string? PaymentType);

/// <summary>The <see cref="CustomerHistoryEntryDto.Type"/> discriminator values.</summary>
public static class CustomerHistoryEntryType
{
    public const string InitialDebt = "initialDebt";
    public const string Sale = "sale";
    public const string Payment = "payment";
}
