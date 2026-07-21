namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.UpdateCustomer;

/// <summary>
/// Edits a customer's details (name/phone/note). <see cref="Id"/> comes from the route. Debt is never
/// changed here — it only moves through credit sales and payments.
/// </summary>
public sealed record UpdateCustomerCommand(Guid Id, string Name, string? Phone, string? Note);
