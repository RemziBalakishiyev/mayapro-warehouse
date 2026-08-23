using System.Reflection;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.CreateEmployee;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetEmployees;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.SetEmployeeActive;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#57 — the payroll register's own use cases. The theme running through every test here is the split:
/// an employee is a person the shop pays, not an account the shop lets in, so there is no password, no role,
/// no unique phone and no delete.
/// </summary>
public sealed class EmployeeHandlerTests
{
    private static CreateEmployeeHandler Creator(AuthDbContext db, FakeActivityLogger? log = null) =>
        new(db, new FakeUnitOfWork(db), new CreateEmployeeValidator(), log ?? new FakeActivityLogger(),
            new FakeCurrentUser(Guid.NewGuid()));

    private static UpdateEmployeeHandler Updater(AuthDbContext db) =>
        new(db, new FakeUnitOfWork(db), new UpdateEmployeeValidator(), new FakeActivityLogger(),
            new FakeCurrentUser(Guid.NewGuid()));

    private static SetEmployeeActiveHandler ActiveSetter(AuthDbContext db, FakeActivityLogger? log = null) =>
        new(db, new FakeUnitOfWork(db), log ?? new FakeActivityLogger(), new FakeCurrentUser(Guid.NewGuid()));

    /// <summary>TC-1 — the happy path: a name and a job title are all it takes to put someone on the payroll.</summary>
    [Fact]
    public async Task Create_Stores_The_Employee_With_Sensible_Defaults()
    {
        await using AuthDbContext db = AuthTestDb.New();
        var log = new FakeActivityLogger();

        var result = await Creator(db, log).Handle(
            new CreateEmployeeCommand("Aysel Məmmədova", null, "Satıcı"), default);

        Assert.True(result.IsSuccess);
        EmployeeDto dto = result.Value;
        Assert.Equal("Aysel Məmmədova", dto.FullName);
        Assert.Equal("Satıcı", dto.Position);
        Assert.Null(dto.Phone);
        Assert.Null(dto.Note);
        Assert.Equal(0m, dto.MonthlySalary);
        Assert.True(dto.IsActive);

        Assert.Single(db.Employees);

        // AC-11: the register and its activity entry are written in one committed transaction.
        (string type, string message) = Assert.Single(log.Entries);
        Assert.Equal("İşçi əlavə etdi", type);
        Assert.Contains("Aysel Məmmədova", message);
    }

    /// <summary>
    /// TC-6 — the load-bearing difference from <c>Users</c>: <c>Employees.Phone</c> carries no unique index,
    /// so two people on one number are two ordinary employees, not a 409.
    /// </summary>
    [Fact]
    public async Task Two_Employees_May_Share_One_Phone_Number()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var first = await Creator(db).Handle(
            new CreateEmployeeCommand("Kamran Səfərov", "0501234567", "Fəhlə"), default);
        var second = await Creator(db).Handle(
            new CreateEmployeeCommand("Orxan Səfərov", "0501234567", "Sürücü"), default);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.Equal("994501234567", first.Value.Phone);
        Assert.Equal("994501234567", second.Value.Phone);
    }

    /// <summary>TC-7 / TC-8 — an optional phone is NULL when blank, canonical when given, 400 when unreadable.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("0501234567", "994501234567")]
    [InlineData("+994 50 123 45 67", "994501234567")]
    public async Task Phone_Is_Optional_And_Stored_Canonically(string? input, string? expected)
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await Creator(db).Handle(
            new CreateEmployeeCommand("Kamran Səfərov", input, "Fəhlə"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Phone);
        Assert.Equal(expected, (await db.Employees.SingleAsync()).Phone);
    }

    /// <summary>TC-8(b) — optional does not mean lenient: a value that is present but unreadable is refused.</summary>
    [Fact]
    public async Task An_Unreadable_Phone_Is_Rejected_And_Nothing_Is_Written()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await Creator(db).Handle(
            new CreateEmployeeCommand("Kamran Səfərov", "abc", "Fəhlə"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(PhoneNormalizer.InvalidFormatMessage, result.Error.Message);
        Assert.Empty(db.Employees);
    }

    /// <summary>TC-10 — every bad field is a 400 in Azerbaijani, and nothing reaches the table.</summary>
    [Theory]
    [InlineData("", "Satıcı", 0)]
    [InlineData("Aysel", "", 0)]
    [InlineData("Aysel", "Satıcı", -5)]
    public async Task Invalid_Input_Is_Rejected(string fullName, string position, decimal salary)
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await Creator(db).Handle(
            new CreateEmployeeCommand(fullName, null, position, salary), default);

        Assert.True(result.IsFailure);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
        Assert.Empty(db.Employees);
    }

    /// <summary>TC-10 — a note longer than the column is caught here (400), not by the database (500).</summary>
    [Fact]
    public async Task An_Over_Long_Note_Is_Rejected()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var result = await Creator(db).Handle(
            new CreateEmployeeCommand("Aysel Məmmədova", null, "Satıcı", 0m, new string('a', 501)), default);

        Assert.True(result.IsFailure);
        Assert.Empty(db.Employees);
    }

    /// <summary>The edit rewrites the details and leaves the salary history alone.</summary>
    [Fact]
    public async Task Update_Rewrites_The_Details_And_Keeps_The_Salary_History()
    {
        await using AuthDbContext db = AuthTestDb.New();
        Employee employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, "2026-03");

        var result = await Updater(db).Handle(
            new UpdateEmployeeCommand(employee.Id, "Günel Quliyeva-Əliyeva", "0559998877", "Menecer", 750m, "Terfi"),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Günel Quliyeva-Əliyeva", result.Value.FullName);
        Assert.Equal("Menecer", result.Value.Position);
        Assert.Equal("994559998877", result.Value.Phone);
        Assert.Equal(750m, result.Value.MonthlySalary);
        Assert.Equal("Terfi", result.Value.Note);

        // The line is untouched — renaming somebody never rewrites what they were already paid.
        SalaryEntry entry = await db.SalaryEntries.SingleAsync();
        Assert.Equal(100m, entry.Amount);
        Assert.Equal(employee.Id, entry.EmployeeId);
    }

    /// <summary>TC-11 — an id that is not on the payroll is "İşçi tapılmadı", on every route.</summary>
    [Fact]
    public async Task Unknown_Employee_Is_Not_Found()
    {
        await using AuthDbContext db = AuthTestDb.New();

        var updated = await Updater(db).Handle(
            new UpdateEmployeeCommand(Guid.NewGuid(), "Aysel", null, "Satıcı"), default);
        var deactivated = await ActiveSetter(db).Handle(Guid.NewGuid(), isActive: false, default);

        Assert.Equal(EmployeeErrors.NotFound, updated.Error);
        Assert.Equal(EmployeeErrors.NotFound, deactivated.Error);
        Assert.Equal("Employee.NotFound", EmployeeErrors.NotFound.Code);
        Assert.Equal("İşçi tapılmadı", EmployeeErrors.NotFound.Message);
    }

    /// <summary>
    /// TC-4 — deactivating is not deleting: the row survives, keeps its salary history and keeps appearing in
    /// the listing with the flag down. Both directions are idempotent.
    /// </summary>
    [Fact]
    public async Task Deactivate_Keeps_The_Row_And_Is_Idempotent()
    {
        await using AuthDbContext db = AuthTestDb.New();
        Employee employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, "2026-03");

        var first = await ActiveSetter(db).Handle(employee.Id, isActive: false, default);
        Assert.True(first.IsSuccess);
        Assert.False(first.Value.IsActive);

        // Still listed, still carrying its history.
        EmployeeDto listed = Assert.Single(await new GetEmployeesHandler(db).Handle(default));
        Assert.Equal(employee.Id, listed.Id);
        Assert.False(listed.IsActive);
        Assert.Single(db.SalaryEntries);

        // Repeating the same statement is still a 200 — the caller asked for an end state, not a toggle.
        var again = await ActiveSetter(db).Handle(employee.Id, isActive: false, default);
        Assert.True(again.IsSuccess);
        Assert.False(again.Value.IsActive);

        var back = await ActiveSetter(db).Handle(employee.Id, isActive: true, default);
        Assert.True(back.Value.IsActive);
        Assert.True((await db.Employees.SingleAsync()).IsActive);
    }

    /// <summary>Both directions are written to the activity feed, in Azerbaijani, with the employee's name.</summary>
    [Fact]
    public async Task Activation_Changes_Are_Logged()
    {
        await using AuthDbContext db = AuthTestDb.New();
        Employee employee = await db.AddEmployeeAsync();
        var log = new FakeActivityLogger();

        await ActiveSetter(db, log).Handle(employee.Id, isActive: false, default);
        await ActiveSetter(db, log).Handle(employee.Id, isActive: true, default);

        Assert.Equal("İşçini deaktiv etdi", log.Entries[0].Type);
        Assert.Equal("İşçini aktiv etdi", log.Entries[1].Type);
        Assert.All(log.Entries, e => Assert.Contains("Günel Quliyeva", e.Message));
    }

    /// <summary>AC-2 / TC-19 — there is no way to erase a payroll record, by design.</summary>
    [Fact]
    public void No_Handler_Deletes_An_Employee()
    {
        string[] deleters = typeof(CreateEmployeeHandler).Assembly
            .GetTypes()
            .Where(t => t.Name.Contains("Employee", StringComparison.Ordinal)
                     && t.Name.StartsWith("Delete", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToArray();

        Assert.Empty(deleters);
    }

    /// <summary>
    /// AC-1 / TC-20 — the shape of the entity is the contract: an employee carries nothing that could be used
    /// to sign in. A future "just add a password here" is meant to turn this test red.
    /// </summary>
    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("Role")]
    [InlineData("Email")]
    public void An_Employee_Carries_No_Login_Fields(string forbidden)
    {
        PropertyInfo? property = typeof(Employee).GetProperty(
            forbidden, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Null(property);
    }

    /// <summary>The same rule at the storage level: the column does not exist either.</summary>
    [Fact]
    public void The_Employees_Table_Has_No_Login_Columns()
    {
        using AuthDbContext db = AuthTestDb.New();

        string[] columns = db.Model.FindEntityType(typeof(Employee))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("PasswordHash", columns);
        Assert.DoesNotContain("Role", columns);
        Assert.DoesNotContain("Email", columns);
        Assert.Contains("Position", columns);
        Assert.Contains("MonthlySalary", columns);
    }

    /// <summary>
    /// AC-1 — and the mirror image on the storage side: <c>Users.Phone</c> is unique inside a shop because it
    /// is a login identifier, while <c>Employees.Phone</c> deliberately has no unique index at all.
    /// </summary>
    [Fact]
    public void Only_The_Login_Phone_Is_Unique()
    {
        using AuthDbContext db = AuthTestDb.New();

        bool employeePhoneUnique = db.Model.FindEntityType(typeof(Employee))!
            .GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Employee.Phone)));

        bool userPhoneUnique = db.Model.FindEntityType(typeof(User))!
            .GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(User.Phone)));

        Assert.False(employeePhoneUnique);
        Assert.True(userPhoneUnique);
    }

    /// <summary>AC-5 — the login account lost its salary: that figure lives on the payroll record now.</summary>
    [Fact]
    public void A_Login_Account_No_Longer_Carries_A_Salary()
    {
        Assert.Null(typeof(User).GetProperty("MonthlySalary"));
        Assert.Null(typeof(User).GetMethod("SetMonthlySalary"));
        Assert.NotNull(typeof(Employee).GetProperty(nameof(Employee.MonthlySalary)));
    }
}
