namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;

/// <summary>
/// Input for creating a customer. Optional opening <see cref="InitialDebt"/> (the debt the customer already
/// owed when migrated into the system) defaults to zero; when positive it is recorded as an auditable
/// opening-balance adjustment alongside the customer.
/// </summary>
public sealed record CreateCustomerCommand(string Name, string? Phone, string? Note, decimal InitialDebt = 0);
