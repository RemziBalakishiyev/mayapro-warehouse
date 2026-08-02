using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetSalarySummary;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#28 — the salary summary's arithmetic in isolation: totals are per month, the remainder is
/// <c>salary − paid − deducted</c>, and it is allowed to go negative.
/// </summary>
public sealed class GetSalarySummaryHandlerTests
{
    private const string March = "2026-03";
    private const string April = "2026-04";

    private static GetSalarySummaryHandler Handler(AuthDbContext db) => new(db, new FakeDateProvider());

    /// <summary>TC2 — the task's headline scenario: 600 salary, 100 + 50 paid, 30 deducted → 420 left.</summary>
    [Fact]
    public async Task Remaining_Is_Salary_Minus_Payments_Minus_Deductions()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, March);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 50m, March);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 30m, March);

        var result = await Handler(db).Handle(March, default);

        Assert.True(result.IsSuccess);
        EmployeeSalarySummaryDto row = Assert.Single(result.Value);
        Assert.Equal(600m, row.MonthlySalary);
        Assert.Equal(150m, row.PaidTotal);
        Assert.Equal(30m, row.DeductionTotal);
        Assert.Equal(420m, row.Remaining);
    }

    /// <summary>TC8 — the filter is the entry's accounting month; other months never leak in.</summary>
    [Fact]
    public async Task Months_Are_Not_Mixed()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, March);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 250m, April);

        var march = await Handler(db).Handle(March, default);
        var april = await Handler(db).Handle(April, default);

        Assert.Equal(100m, Assert.Single(march.Value).PaidTotal);
        Assert.Equal(250m, Assert.Single(april.Value).PaidTotal);
    }

    /// <summary>TC10 — an employee with no lines at all is still listed, with the full salary outstanding.</summary>
    [Fact]
    public async Task Employee_Without_Entries_Is_Listed_With_Zero_Totals()
    {
        await using AuthDbContext db = AuthTestDb.New();
        await db.AddEmployeeAsync(monthlySalary: 400m);

        var result = await Handler(db).Handle(March, default);

        EmployeeSalarySummaryDto row = Assert.Single(result.Value);
        Assert.Equal(0m, row.PaidTotal);
        Assert.Equal(0m, row.DeductionTotal);
        Assert.Equal(400m, row.Remaining);
    }

    /// <summary>TC11 — overpaying is a negative remainder, not an error and not a clamp to zero.</summary>
    [Fact]
    public async Task Remaining_May_Be_Negative_When_Overpaid()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 700m, March);

        var result = await Handler(db).Handle(March, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(-100m, Assert.Single(result.Value).Remaining);
    }

    /// <summary>An omitted month means the current business month, not "all months".</summary>
    [Fact]
    public async Task Omitted_Month_Defaults_To_The_Current_Business_Month()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync(monthlySalary: 600m);
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 90m, "2026-08");   // FakeDateProvider = 2026-08-01
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 999m, March);

        var result = await Handler(db).Handle(null, default);

        Assert.Equal(90m, Assert.Single(result.Value).PaidTotal);
    }

    /// <summary>TC23 — a malformed month is rejected, never silently ignored.</summary>
    [Theory]
    [InlineData("2026-13")]
    [InlineData("avqust")]
    [InlineData("26-8")]
    public async Task Malformed_Month_Is_Rejected(string month)
    {
        await using AuthDbContext db = AuthTestDb.New();
        await db.AddEmployeeAsync();

        var result = await Handler(db).Handle(month, default);

        Assert.True(result.IsFailure);
        Assert.Equal(SalaryErrors.InvalidMonth, result.Error);
    }

    /// <summary>Each employee is folded separately — one person's lines never reach another's row.</summary>
    [Fact]
    public async Task Totals_Are_Per_Employee()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User a = await db.AddEmployeeAsync("Günel Quliyeva", "0554445566", monthlySalary: 600m);
        User b = await db.AddEmployeeAsync("Elvin Hüseynov", "0553334455", monthlySalary: 500m);
        await db.AddEntryAsync(a.Id, SalaryEntryType.Payment, 100m, March);
        await db.AddEntryAsync(b.Id, SalaryEntryType.Deduction, 20m, March);

        var result = await Handler(db).Handle(March, default);

        EmployeeSalarySummaryDto rowA = result.Value.Single(r => r.UserId == a.Id);
        EmployeeSalarySummaryDto rowB = result.Value.Single(r => r.UserId == b.Id);
        Assert.Equal(500m, rowA.Remaining);   // 600 − 100
        Assert.Equal(0m, rowA.DeductionTotal);
        Assert.Equal(480m, rowB.Remaining);   // 500 − 20
        Assert.Equal(0m, rowB.PaidTotal);
    }
}
