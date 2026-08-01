using MayaPro.WarehouseApi.Modules.Customers.Application;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.Modules.Customers.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Customers.Tests;

/// <summary>
/// Unit tests for <see cref="CustomersModuleContract.GetDebtorsAsync"/> and
/// <see cref="CustomersModuleContract.GetPaymentsAsync"/> (BE#27, AC-G6) — the new <c>ICustomersModule</c>
/// contract members the debts-kpi endpoint reads back debtor breakdown and period collections through. Uses
/// a real (in-memory) <see cref="CustomersDbContext"/> so the SQL filters are exercised, not just the
/// calculator.
/// </summary>
public sealed class CustomersModuleContractTests
{
    private static readonly DateOnly Today = new(2026, 8, 2);

    [Fact]
    public async Task GetDebtorsAsync_Returns_Only_Customers_With_Positive_Debt()
    {
        await using CustomersDbContext db = NewDb();
        AddCustomer(db, "Əli", debt: 500m);
        AddCustomer(db, "Vəli", debt: 0m);       // fully paid — not a debtor
        AddCustomer(db, "Səda", debt: 50m);
        await db.SaveChangesAsync();

        var contract = new CustomersModuleContract(db, new FixedDateProvider(Today));

        IReadOnlyList<CustomerDebtRow> rows = await contract.GetDebtorsAsync(default);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Name == "Əli" && r.Debt == 500m);
        Assert.Contains(rows, r => r.Name == "Səda" && r.Debt == 50m);
        Assert.DoesNotContain(rows, r => r.Name == "Vəli");
    }

    [Fact]
    public async Task GetDebtorsAsync_Returns_Empty_When_No_Customer_Owes_Anything()
    {
        await using CustomersDbContext db = NewDb();
        AddCustomer(db, "Vəli", debt: 0m);
        await db.SaveChangesAsync();

        var contract = new CustomersModuleContract(db, new FixedDateProvider(Today));

        IReadOnlyList<CustomerDebtRow> rows = await contract.GetDebtorsAsync(default);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetPaymentsAsync_Returns_Only_Payments_Within_The_Inclusive_Range()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Əli", debt: 100m);
        AddPayment(db, customer.Id, 150m, Today.AddDays(-1));
        AddPayment(db, customer.Id, 80m, Today);
        AddPayment(db, customer.Id, 30m, Today.AddDays(1)); // outside the range
        await db.SaveChangesAsync();

        var contract = new CustomersModuleContract(db, new FixedDateProvider(Today));

        IReadOnlyList<CustomerPaymentRow> rows =
            await contract.GetPaymentsAsync(Today.AddDays(-1), Today, default);

        Assert.Equal(2, rows.Count);
        Assert.Equal(230m, rows.Sum(r => r.Amount));
    }

    [Fact]
    public async Task GetPaymentsAsync_Empty_From_To_Returns_Every_Payment()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Əli", debt: 100m);
        AddPayment(db, customer.Id, 150m, Today.AddDays(-100));
        AddPayment(db, customer.Id, 80m, Today.AddDays(50));
        await db.SaveChangesAsync();

        var contract = new CustomersModuleContract(db, new FixedDateProvider(Today));

        IReadOnlyList<CustomerPaymentRow> rows = await contract.GetPaymentsAsync(null, null, default);

        Assert.Equal(2, rows.Count);
    }

    private static Customer AddCustomer(CustomersDbContext db, string name, decimal debt)
    {
        Customer customer = Customer.Create(name, phone: null, note: null, debt: debt);
        db.Customers.Add(customer);
        return customer;
    }

    private static void AddPayment(CustomersDbContext db, Guid customerId, decimal amount, DateOnly date)
    {
        CustomerPayment payment = CustomerPayment.Create(customerId, amount, note: null, receivedByUserId: null);
        db.CustomerPayments.Add(payment);
        db.Entry(payment).Property(p => p.Date).CurrentValue = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    private static CustomersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase($"customers-contract-tests-{Guid.NewGuid()}")
            .Options;
        return new CustomersDbContext(options);
    }
}
