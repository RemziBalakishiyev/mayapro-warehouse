namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>A customer payment as returned by the API.</summary>
public sealed record CustomerPaymentDto(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    string? Note,
    Guid? ReceivedByUserId,
    DateTime Date);
