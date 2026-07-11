namespace MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.CreateCustomer;

/// <summary>Input for creating a customer. Optional opening <see cref="Debt"/> defaults to zero.</summary>
public sealed record CreateCustomerCommand(string Name, string? Phone, string? Note, decimal Debt = 0);
