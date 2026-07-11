namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.AddCustomerPayment;

/// <summary>A payment against a customer's debt. <see cref="CustomerId"/> comes from the route.</summary>
public sealed record AddCustomerPaymentCommand(Guid CustomerId, decimal Amount, string? Note);
