using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.DeleteSalaryEntry;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalaryEntries;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeSalary;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#28 — the salary entry use cases: creating, listing and deleting a line, and setting the agreed salary.
/// </summary>
public sealed class SalaryEntryHandlerTests
{
    private const string Payment = WireFormat.SalaryEntryTypes.Payment;
    private const string Deduction = WireFormat.SalaryEntryTypes.Deduction;
    private const string March = "2026-03";

    // 2026-08-01 10:00 UTC — "today" for every handler below.
    private static readonly FakeDateProvider Clock = new();

    private static (CreateSalaryEntryHandler Handler, FakeActivityLogger Log, FakeUnitOfWork Uow) Creator(AuthDbContext db)
    {
        var log = new FakeActivityLogger();
        var uow = new FakeUnitOfWork(db);
        return (
            new CreateSalaryEntryHandler(db, uow, new CreateSalaryEntryValidator(), log, new FakeCurrentUser(Guid.NewGuid()), Clock),
            log,
            uow);
    }

    /// <summary>AC3 — the happy path stores every field and answers with the created line.</summary>
    [Fact]
    public async Task Create_Stores_The_Line_And_Returns_It()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, FakeActivityLogger log, FakeUnitOfWork uow) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, Payment, 100m, "Avans", "2026-08"), default);

        Assert.True(result.IsSuccess);
        SalaryEntryDto dto = result.Value;
        Assert.Equal(employee.Id, dto.UserId);
        Assert.Equal(Payment, dto.Type);
        Assert.Equal(100m, dto.Amount);
        Assert.Equal("Avans", dto.Note);
        Assert.Equal("2026-08", dto.Month);
        Assert.Equal(Clock.UtcNow, dto.Date);

        SalaryEntry stored = await db.SalaryEntries.SingleAsync();
        Assert.Equal(SalaryEntryType.Payment, stored.Type);

        // AC5: the line and its activity log are written inside one committed transaction.
        Assert.True(uow.Committed);
        (string type, string message) = Assert.Single(log.Entries);
        Assert.Equal("Maaş əməliyyatı", type);
        Assert.Contains("Günel Quliyeva", message);
        Assert.Contains("100", message);
    }

    /// <summary>AC3 — an omitted month falls back to the current business month.</summary>
    [Fact]
    public async Task Create_Without_Month_Uses_The_Current_Business_Month()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, _, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, Payment, 40m, null, null), default);

        Assert.Equal("2026-08", result.Value.Month);
    }

    /// <summary>
    /// AC4 — paying last month's salary today writes today's date with last month's accounting month; the
    /// two fields never overwrite one another.
    /// </summary>
    [Fact]
    public async Task Create_Keeps_Cash_Date_And_Accounting_Month_Independent()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, _, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, Payment, 80m, null, "2026-07"), default);

        Assert.Equal("2026-07", result.Value.Month);
        Assert.Equal(new DateOnly(2026, 8, 1), DateOnly.FromDateTime(result.Value.Date));
    }

    /// <summary>TC21 — an unknown type is its own error code, and nothing is written.</summary>
    [Fact]
    public async Task Create_With_Unknown_Type_Is_Rejected()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, FakeActivityLogger log, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, "bonus", 10m, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(SalaryErrors.InvalidType, result.Error);
        Assert.Empty(db.SalaryEntries);
        Assert.Empty(log.Entries);
    }

    /// <summary>TC22 — a non-positive amount is rejected before anything is written.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Create_With_Non_Positive_Amount_Is_Rejected(decimal amount)
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, _, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, Payment, amount, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Empty(db.SalaryEntries);
    }

    /// <summary>TC23 — a malformed month in the body is rejected, not silently replaced by "this month".</summary>
    [Fact]
    public async Task Create_With_Malformed_Month_Is_Rejected()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        (CreateSalaryEntryHandler handler, _, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(employee.Id, Payment, 10m, null, "26-8"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(SalaryErrors.InvalidMonth, result.Error);
        Assert.Empty(db.SalaryEntries);
    }

    /// <summary>TC19 — an unknown employee is a 404-shaped error, and no orphan line is written.</summary>
    [Fact]
    public async Task Create_For_Unknown_Employee_Is_Not_Found()
    {
        await using AuthDbContext db = AuthTestDb.New();
        (CreateSalaryEntryHandler handler, _, _) = Creator(db);

        var result = await handler.Handle(
            new CreateSalaryEntryCommand(Guid.NewGuid(), Payment, 10m, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserNotFound, result.Error);
        Assert.Empty(db.SalaryEntries);
    }

    /// <summary>AC6 — the listing is filtered by month and ordered newest first.</summary>
    [Fact]
    public async Task Entries_Are_Filtered_By_Month_Newest_First()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, March, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 30m, March, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 250m, "2026-04");

        var result = await new GetSalaryEntriesHandler(db, Clock).Handle(employee.Id, March, default);

        Assert.True(result.IsSuccess);
        Assert.Equal([30m, 100m], result.Value.Select(e => e.Amount));
        Assert.Equal(Deduction, result.Value[0].Type);
    }

    /// <summary>TC12 — a month with no lines is an empty list, not a 404 and not null.</summary>
    [Fact]
    public async Task Entries_For_An_Empty_Month_Are_An_Empty_List()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();

        var result = await new GetSalaryEntriesHandler(db, Clock).Handle(employee.Id, "2030-01", default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    /// <summary>TC19 — listing an unknown employee's lines is not found, not an empty list.</summary>
    [Fact]
    public async Task Entries_For_Unknown_Employee_Are_Not_Found()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await new GetSalaryEntriesHandler(db, Clock).Handle(Guid.NewGuid(), March, default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserNotFound, result.Error);
    }

    /// <summary>AC7 — the owner's delete removes the line and logs it in the same transaction.</summary>
    [Fact]
    public async Task Delete_Removes_The_Line_And_Logs_It()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, March);
        SalaryEntry entry = await db.SalaryEntries.SingleAsync();

        var log = new FakeActivityLogger();
        var uow = new FakeUnitOfWork(db);
        var result = await new DeleteSalaryEntryHandler(db, uow, log, new FakeCurrentUser(Guid.NewGuid()))
            .Handle(employee.Id, entry.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.SalaryEntries);
        Assert.True(uow.Committed);
        Assert.Single(log.Entries);
    }

    /// <summary>
    /// TC20 — an entry belonging to employee A cannot be deleted (or even confirmed) through employee B's
    /// route: it is simply not found, and the row survives.
    /// </summary>
    [Fact]
    public async Task Delete_Through_Another_Employees_Route_Is_Not_Found()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User a = await db.AddEmployeeAsync("Günel Quliyeva", "0554445566");
        User b = await db.AddEmployeeAsync("Elvin Hüseynov", "0553334455");
        await db.AddEntryAsync(a.Id, SalaryEntryType.Payment, 100m, March);
        SalaryEntry entry = await db.SalaryEntries.SingleAsync();

        var result = await new DeleteSalaryEntryHandler(db, new FakeUnitOfWork(db), new FakeActivityLogger(), new FakeCurrentUser())
            .Handle(b.Id, entry.Id, default);

        Assert.True(result.IsFailure);
        Assert.Equal(SalaryErrors.EntryNotFound, result.Error);
        Assert.Single(db.SalaryEntries);
    }

    /// <summary>AC2 — the agreed salary is stored and echoed back on the employee row.</summary>
    [Fact]
    public async Task Set_Salary_Stores_The_Amount()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();

        var result = await new SetEmployeeSalaryHandler(db, new SetEmployeeSalaryValidator())
            .Handle(new SetEmployeeSalaryCommand(employee.Id, 600m), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, result.Value.MonthlySalary);
        Assert.Equal(600m, (await db.Users.SingleAsync()).MonthlySalary);
    }

    /// <summary>TC24 — a negative salary is rejected and the stored value is untouched.</summary>
    [Fact]
    public async Task Set_Negative_Salary_Is_Rejected()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync(monthlySalary: 600m);

        var result = await new SetEmployeeSalaryHandler(db, new SetEmployeeSalaryValidator())
            .Handle(new SetEmployeeSalaryCommand(employee.Id, -1m), default);

        Assert.True(result.IsFailure);
        Assert.Equal(600m, (await db.Users.SingleAsync()).MonthlySalary);
    }

    /// <summary>TC19 — setting the salary of an employee who does not exist is not found.</summary>
    [Fact]
    public async Task Set_Salary_For_Unknown_Employee_Is_Not_Found()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await new SetEmployeeSalaryHandler(db, new SetEmployeeSalaryValidator())
            .Handle(new SetEmployeeSalaryCommand(Guid.NewGuid(), 600m), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserNotFound, result.Error);
    }
}
