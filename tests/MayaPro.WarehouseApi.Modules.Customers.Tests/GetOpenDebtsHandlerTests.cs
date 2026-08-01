using MayaPro.WarehouseApi.Modules.Customers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Customers.Application.UseCases.GetOpenDebts;
using MayaPro.WarehouseApi.Modules.Customers.Domain;
using MayaPro.WarehouseApi.Modules.Customers.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.Modules.Customers.Tests;

/// <summary>
/// BE#21 — unit tests for <see cref="GetOpenDebtsHandler"/>: the FIFO write-off of a customer's payments
/// against their debt sources (opening balance + sales that left a remaining balance), oldest first.
/// </summary>
public sealed class GetOpenDebtsHandlerTests
{
    private static readonly DateOnly Today = new(2026, 2, 1);
    private static readonly DateTime Jan1 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// TC1 — customer with two still-owing sales (200 then 300) and a 150 payment: FIFO eats the oldest
    /// source first, so it is left owing 50 while the newer one is untouched at 300.
    /// </summary>
    [Fact]
    public async Task Payments_Are_Written_Off_Against_The_Oldest_Source_First()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Müştəri A", debt: 350m, phone: "0501112233");
        AddPayment(db, customer.Id, 150m, Jan1.AddDays(5));
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(
            db,
            Sale(customer.Id, Jan1, "Kabel", 2, remaining: 200m),
            Sale(customer.Id, Jan1.AddDays(2), "Rozetka", 3, remaining: 300m));

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Equal(2, result.Items.Count);

        OpenDebtDto oldest = result.Items[0];
        Assert.Equal(CustomerHistoryEntryType.Sale, oldest.Source);
        Assert.Equal("Kabel × 2", oldest.Description);
        Assert.Equal(200m, oldest.OriginalAmount);
        Assert.Equal(150m, oldest.PaidSoFar);
        Assert.Equal(50m, oldest.Remaining);

        OpenDebtDto newest = result.Items[1];
        Assert.Equal("Rozetka × 3", newest.Description);
        Assert.Equal(300m, newest.OriginalAmount);
        Assert.Equal(0m, newest.PaidSoFar);
        Assert.Equal(300m, newest.Remaining);

        // Oldest debt first, and the customer's identity travels on every row.
        Assert.True(oldest.SourceDate < newest.SourceDate);
        Assert.Equal(customer.Id, oldest.CustomerId);
        Assert.Equal("Müştəri A", oldest.CustomerName);
        Assert.Equal("0501112233", oldest.Phone);
    }

    /// <summary>TC2 — a source the payments have fully covered is settled history, not open debt.</summary>
    [Fact]
    public async Task Fully_Paid_Sources_Are_Excluded()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Müştəri B", debt: 300m);
        AddPayment(db, customer.Id, 200m, Jan1.AddDays(5));
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(
            db,
            Sale(customer.Id, Jan1, "Kabel", 2, remaining: 200m),
            Sale(customer.Id, Jan1.AddDays(2), "Rozetka", 3, remaining: 300m));

        OpenDebtsDto result = await handler.Handle(default);

        OpenDebtDto row = Assert.Single(result.Items);
        Assert.Equal("Rozetka × 3", row.Description);
        Assert.Equal(300m, row.Remaining);
    }

    /// <summary>TC3 — the remaining amounts must reconstruct the customer's stored debt, with no warning.</summary>
    [Fact]
    public async Task Remaining_Sum_Equals_The_Customers_Debt_And_Logs_No_Warning()
    {
        await using CustomersDbContext db = NewDb();
        // 100 opening balance + 200 + 300 owing sales − 150 paid = 450 debt.
        Customer customer = AddCustomer(db, "Müştəri C", debt: 450m);
        AddInitialDebt(db, customer.Id, 100m, Jan1.AddDays(-1));
        AddPayment(db, customer.Id, 150m, Jan1.AddDays(5));
        await db.SaveChangesAsync();

        var logger = new FakeLogger<GetOpenDebtsHandler>();
        GetOpenDebtsHandler handler = NewHandler(
            db,
            logger,
            Sale(customer.Id, Jan1, "Kabel", 2, remaining: 200m),
            Sale(customer.Id, Jan1.AddDays(2), "Rozetka", 3, remaining: 300m));

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Equal(customer.Debt, result.Items.Sum(r => r.Remaining));
        Assert.Equal(customer.Debt, result.TotalRemaining);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>The opening balance is a debt source of its own, described as "İlkin borc".</summary>
    [Fact]
    public async Task Initial_Debt_Is_Listed_As_Its_Own_Source()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Müştəri D", debt: 120m);
        AddInitialDebt(db, customer.Id, 120m, Jan1);
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(db);

        OpenDebtsDto result = await handler.Handle(default);

        OpenDebtDto row = Assert.Single(result.Items);
        Assert.Equal(CustomerHistoryEntryType.InitialDebt, row.Source);
        Assert.Equal(GetOpenDebtsHandler.InitialDebtDescription, row.Description);
        Assert.Equal(120m, row.OriginalAmount);
        Assert.Equal(0m, row.PaidSoFar);
        Assert.Equal(120m, row.Remaining);
        Assert.Equal(31, row.DaysOld); // 1 Jan → 1 Feb, business-zone whole days
    }

    /// <summary>A payment is written off against the opening balance before any later sale.</summary>
    [Fact]
    public async Task Initial_Debt_Is_Paid_Down_Before_Later_Sales()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Müştəri E", debt: 180m);
        AddInitialDebt(db, customer.Id, 100m, Jan1);
        AddPayment(db, customer.Id, 120m, Jan1.AddDays(4));
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(db, Sale(customer.Id, Jan1.AddDays(2), "Kabel", 1, remaining: 200m));

        OpenDebtsDto result = await handler.Handle(default);

        // 100 opening fully covered → gone; the remaining 20 lands on the sale.
        OpenDebtDto row = Assert.Single(result.Items);
        Assert.Equal(CustomerHistoryEntryType.Sale, row.Source);
        Assert.Equal(20m, row.PaidSoFar);
        Assert.Equal(180m, row.Remaining);
        Assert.Equal(180m, result.TotalRemaining);
    }

    /// <summary>Rows from several customers are interleaved by age, and the total covers them all.</summary>
    [Fact]
    public async Task Rows_Of_All_Customers_Are_Ordered_Oldest_First_And_Summed()
    {
        await using CustomersDbContext db = NewDb();
        Customer first = AddCustomer(db, "Müştəri A", debt: 100m);
        Customer second = AddCustomer(db, "Müştəri B", debt: 50m);
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(
            db,
            Sale(second.Id, Jan1.AddDays(3), "Rozetka", 1, remaining: 50m),
            Sale(first.Id, Jan1, "Kabel", 1, remaining: 100m));

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Equal([first.Id, second.Id], result.Items.Select(r => r.CustomerId));
        Assert.True(result.Items[0].DaysOld > result.Items[1].DaysOld);
        Assert.Equal(150m, result.TotalRemaining);
    }

    /// <summary>
    /// A customer whose stored debt no longer matches their sources is a data-quality problem, not a
    /// request failure: the list is still served and a warning is logged.
    /// </summary>
    [Fact]
    public async Task Debt_Mismatch_Logs_A_Warning_Without_Failing()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Uyğunsuz", debt: 500m); // sources only add up to 200
        await db.SaveChangesAsync();

        var logger = new FakeLogger<GetOpenDebtsHandler>();
        GetOpenDebtsHandler handler = NewHandler(db, logger, Sale(customer.Id, Jan1, "Kabel", 1, remaining: 200m));

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Equal(200m, result.TotalRemaining);
        (LogLevel Level, string Message) warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(customer.Id.ToString(), warning.Message);
    }

    /// <summary>A debt-free customer contributes nothing — and no warning either.</summary>
    [Fact]
    public async Task Customer_Without_Sources_Is_Absent_From_The_List()
    {
        await using CustomersDbContext db = NewDb();
        AddCustomer(db, "Borcsuz", debt: 0m);
        await db.SaveChangesAsync();

        var logger = new FakeLogger<GetOpenDebtsHandler>();
        GetOpenDebtsHandler handler = NewHandler(db, logger);

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.TotalRemaining);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>An owing sale of a since-deleted customer has nobody to bill, so it is dropped.</summary>
    [Fact]
    public async Task Sources_Of_A_Deleted_Customer_Are_Ignored()
    {
        await using CustomersDbContext db = NewDb();
        await db.SaveChangesAsync();

        GetOpenDebtsHandler handler = NewHandler(db, Sale(Guid.NewGuid(), Jan1, "Kabel", 1, remaining: 200m));

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.TotalRemaining);
    }

    /// <summary>
    /// Two sources of the same customer stamped at the exact same instant (a rare but possible tie) must
    /// still be written off in one fixed, reproducible order — not whatever order the sales happened to
    /// arrive in from the Sales module — so the FIFO split can never flip between two identical requests.
    /// </summary>
    [Fact]
    public async Task Sources_Tied_On_The_Same_Instant_Are_Allocated_In_A_Fixed_Deterministic_Order()
    {
        await using CustomersDbContext db = NewDb();
        Customer customer = AddCustomer(db, "Eyni anlıq", debt: 300m);
        AddPayment(db, customer.Id, 80m, Jan1.AddDays(5));
        await db.SaveChangesAsync();

        var lowerId = new Guid("00000000-0000-0000-0000-000000000001");
        var higherId = new Guid("00000000-0000-0000-0000-000000000002");
        CustomerOutstandingSale first = new(lowerId, customer.Id, Jan1, "Kabel", 1, RemainingAmount: 100m);
        CustomerOutstandingSale second = new(higherId, customer.Id, Jan1, "Rozetka", 1, RemainingAmount: 200m);

        // Feed the two tied sources in reverse (higher id first) — the allocation must not depend on that.
        GetOpenDebtsHandler handler = NewHandler(db, second, first);

        OpenDebtsDto result = await handler.Handle(default);

        Assert.Equal(2, result.Items.Count);
        OpenDebtDto lowerIdRow = Assert.Single(result.Items, r => r.Description == "Kabel × 1");
        OpenDebtDto higherIdRow = Assert.Single(result.Items, r => r.Description == "Rozetka × 1");
        Assert.Equal(80m, lowerIdRow.PaidSoFar);
        Assert.Equal(20m, lowerIdRow.Remaining);
        Assert.Equal(0m, higherIdRow.PaidSoFar);
        Assert.Equal(200m, higherIdRow.Remaining);
    }

    private static CustomerOutstandingSale Sale(
        Guid customerId, DateTime date, string productName, int quantity, decimal remaining) =>
        new(Guid.NewGuid(), customerId, date, productName, quantity, remaining);

    private static Customer AddCustomer(CustomersDbContext db, string name, decimal debt, string? phone = null)
    {
        Customer customer = Customer.Create(name, phone, note: null, debt: debt);
        db.Customers.Add(customer);
        return customer;
    }

    private static void AddPayment(CustomersDbContext db, Guid customerId, decimal amount, DateTime date)
    {
        CustomerPayment payment = CustomerPayment.Create(customerId, amount, null, null);
        db.CustomerPayments.Add(payment);
        db.Entry(payment).Property(p => p.Date).CurrentValue = date;
    }

    private static void AddInitialDebt(CustomersDbContext db, Guid customerId, decimal amount, DateTime date)
    {
        CustomerDebtAdjustment adjustment =
            CustomerDebtAdjustment.Create(customerId, amount, CustomerDebtAdjustment.InitialDebtNote, null);
        db.CustomerDebtAdjustments.Add(adjustment);
        db.Entry(adjustment).Property(a => a.Date).CurrentValue = date;
    }

    private static GetOpenDebtsHandler NewHandler(
        CustomersDbContext db, params CustomerOutstandingSale[] outstanding) =>
        NewHandler(db, new FakeLogger<GetOpenDebtsHandler>(), outstanding);

    private static GetOpenDebtsHandler NewHandler(
        CustomersDbContext db,
        FakeLogger<GetOpenDebtsHandler> logger,
        params CustomerOutstandingSale[] outstanding) =>
        new(db, new FakeSalesModule(outstanding), new FixedDateProvider(Today), logger);

    private static CustomersDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase($"customers-open-debts-tests-{Guid.NewGuid()}")
            .Options;
        return new CustomersDbContext(options);
    }
}
