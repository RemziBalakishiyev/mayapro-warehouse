using MayaPro.WarehouseApi.Modules.Customers.Domain;

namespace MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;

/// <summary>Maps Customers entities to their wire DTOs.</summary>
public static class CustomerMapping
{
    public static CustomerDto ToDto(
        this Customer customer,
        decimal paidAmount = 0m,
        DateTime? lastPurchaseDate = null,
        DateTime? lastPaymentDate = null) =>
        new(customer.Id, customer.Name, customer.Phone, customer.Note, customer.Debt,
            paidAmount, lastPurchaseDate, lastPaymentDate, customer.CreatedAt, customer.UpdatedAt);

    public static CustomerPaymentDto ToDto(this CustomerPayment payment) =>
        new(payment.Id, payment.CustomerId, payment.Amount, payment.Note,
            payment.ReceivedByUserId, payment.Date);
}
