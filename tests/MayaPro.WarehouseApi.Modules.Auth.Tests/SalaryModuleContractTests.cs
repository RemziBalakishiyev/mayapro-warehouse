using MayaPro.WarehouseApi.Modules.Auth.Application;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#28 — the cross-module contract that feeds day-end and the dashboard. Two rules are load-bearing here:
/// only <c>payment</c> lines are ever reported (a deduction moves no cash), and day boundaries are the
/// business time zone's, not UTC's (ADR-0005).
/// </summary>
public sealed class SalaryModuleContractTests
{
    private const string Month = "2026-08";
    private static readonly DateOnly Day = new(2026, 8, 1);

    private static SalaryModuleContract Contract(AuthDbContext db) => new(db, new FakeDateProvider());

    private static SalaryModuleContract BakuContract(AuthDbContext db) => new(
        db,
        new AppDateProvider(
            TimeZoneInfo.FindSystemTimeZoneById(AppDateProvider.DefaultTimeZoneId),
            () => new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc)));

    private static DateTime At(int day, int hour) => new(2026, 8, day, hour, 0, 0, DateTimeKind.Utc);

    /// <summary>TC6 — the day total sums that day's payments only: not deductions, not another day.</summary>
    [Fact]
    public async Task Day_Total_Sums_Only_That_Days_Payments()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, Month, At(1, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 50m, Month, At(1, 15));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 30m, Month, At(1, 16));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 999m, Month, At(2, 9));

        decimal total = await Contract(db).GetDayPaymentsTotalAsync(Day);

        Assert.Equal(150m, total);   // not 180 (deduction), not 1149 (the next day)
    }

    /// <summary>TC29 — with no salary rows at all the contract contributes nothing anywhere.</summary>
    [Fact]
    public async Task Empty_Table_Yields_Zero_And_No_Rows()
    {
        await using AuthDbContext db = AuthTestDb.New();
        await db.AddEmployeeAsync();

        Assert.Equal(0m, await Contract(db).GetDayPaymentsTotalAsync(Day));
        Assert.Empty(await Contract(db).GetPaymentsAsync(null, null));
    }

    /// <summary>AC11 — a day of nothing but deductions still reports zero cash out.</summary>
    [Fact]
    public async Task Deductions_Alone_Never_Reach_The_Cash_Figures()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 30m, Month, At(1, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 12m, Month, At(1, 11));

        Assert.Equal(0m, await Contract(db).GetDayPaymentsTotalAsync(Day));
        Assert.Empty(await Contract(db).GetPaymentsAsync(null, null));
    }

    /// <summary>
    /// TC7 — a payment stamped 2026-08-01T20:30Z happened at 00:30 on 2 August in Baku, so it belongs to the
    /// 2nd's business day, not the 1st's. This is the whole reason the contract goes through IDateProvider.
    /// </summary>
    [Fact]
    public async Task Day_Boundary_Follows_The_Business_Time_Zone_Not_Utc()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(
            employee.Id, SalaryEntryType.Payment, 70m, Month, new DateTime(2026, 8, 1, 20, 30, 0, DateTimeKind.Utc));

        SalaryModuleContract contract = BakuContract(db);

        Assert.Equal(70m, await contract.GetDayPaymentsTotalAsync(new DateOnly(2026, 8, 2)));
        Assert.Equal(0m, await contract.GetDayPaymentsTotalAsync(new DateOnly(2026, 8, 1)));
    }

    /// <summary>The range query carries the employee's name and skips deductions, oldest first.</summary>
    [Fact]
    public async Task Payments_Are_Reported_With_The_Employee_Name_In_Date_Order()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync("Günel Quliyeva", "0554445566");
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 200m, Month, At(2, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 100m, Month, At(1, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Deduction, 30m, Month, At(1, 10));

        IReadOnlyList<SalaryPaymentRow> rows = await Contract(db).GetPaymentsAsync(null, null);

        Assert.Equal(2, rows.Count);
        Assert.Equal(100m, rows[0].Amount);
        Assert.Equal(200m, rows[1].Amount);
        Assert.All(rows, r => Assert.Equal("Günel Quliyeva", r.FullName));
        Assert.All(rows, r => Assert.Equal(employee.Id, r.UserId));
    }

    /// <summary>Both range bounds are inclusive days, so a bound day's payments are in, neighbours are out.</summary>
    [Fact]
    public async Task Range_Bounds_Are_Inclusive_Days()
    {
        await using AuthDbContext db = AuthTestDb.New();
        User employee = await db.AddEmployeeAsync();
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 10m, Month, At(1, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 20m, Month, At(2, 9));
        await db.AddEntryAsync(employee.Id, SalaryEntryType.Payment, 40m, Month, At(3, 9));

        IReadOnlyList<SalaryPaymentRow> rows = await Contract(db).GetPaymentsAsync(
            new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 3));

        Assert.Equal([20m, 40m], rows.Select(r => r.Amount));
    }
}
