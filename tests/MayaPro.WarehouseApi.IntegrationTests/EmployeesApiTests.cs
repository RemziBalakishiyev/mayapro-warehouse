using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for the employee salary system (BE#28): the agreed monthly salary, the salary account
/// (payments and deductions), the monthly summary, and the fact that a payment is real cash leaving the
/// drawer while a deduction never is.
/// <para>
/// The integration database is shared between test classes, so every test that asserts exact figures does so
/// on the one seeded employee no other test touches (Günel Quliyeva) and in its own accounting month; the
/// cash-side tests measure before/after deltas instead of absolute amounts, the way <c>DayEndApiTests</c>
/// does. Closing the day is deliberately left to <c>DayEndApiTests</c> — a day can only be closed once.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmployeesApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public EmployeesApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// TC1 — the task's headline scenario end to end: 600 agreed, 100 + 50 paid, 30 deducted → 420 left.
    /// </summary>
    [Fact]
    public async Task Salary_Summary_Computes_Paid_Deducted_And_Remaining()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);
        const string Month = "2026-03";

        await SetSalaryAsync(client, employeeId, 600m);
        await AddEntryAsync(client, employeeId, "payment", 100m, Month, "Avans");
        await AddEntryAsync(client, employeeId, "payment", 50m, Month);
        await AddEntryAsync(client, employeeId, "deduction", 30m, Month, "Yemək");

        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(client, employeeId, Month);

        Assert.Equal(600m, row.MonthlySalary);
        Assert.Equal(150m, row.PaidTotal);
        Assert.Equal(30m, row.DeductionTotal);
        Assert.Equal(420m, row.Remaining);
    }

    /// <summary>TC13 / TC14 — <c>monthlySalary</c> is additive on the employee row and defaults to 0.</summary>
    [Fact]
    public async Task Employees_List_Carries_MonthlySalary_And_Defaults_To_Zero()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        await SetSalaryAsync(client, employeeId, 600m);

        List<IntegrationTestHelpers.EmployeeDto> all = await EmployeesAsync(client);
        IntegrationTestHelpers.EmployeeDto paid = all.Single(e => e.Id == employeeId);
        Assert.Equal(600m, paid.MonthlySalary);

        // The pre-existing fields are untouched by the additive change.
        Assert.Equal(IntegrationTestHelpers.SecondSellerPhone, paid.Phone);
        Assert.Equal("satici", paid.Role);
        Assert.True(paid.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(paid.FullName));

        // TC14: an employee nobody has set a salary for reads 0, never null.
        IntegrationTestHelpers.EmployeeDto untouched =
            all.Single(e => e.Phone == IntegrationTestHelpers.ManagerPhone);
        Assert.Equal(0m, untouched.MonthlySalary);
    }

    /// <summary>TC8 — the summary is per accounting month; two months never bleed into each other.</summary>
    [Fact]
    public async Task Months_Are_Kept_Apart()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        await SetSalaryAsync(client, employeeId, 600m);
        await AddEntryAsync(client, employeeId, "payment", 100m, "2026-04");
        await AddEntryAsync(client, employeeId, "payment", 250m, "2026-05");

        Assert.Equal(100m, (await SummaryRowAsync(client, employeeId, "2026-04")).PaidTotal);
        Assert.Equal(250m, (await SummaryRowAsync(client, employeeId, "2026-05")).PaidTotal);

        // The per-employee listing agrees with the summary.
        List<IntegrationTestHelpers.SalaryEntryDto> april = await EntriesAsync(client, employeeId, "2026-04");
        Assert.Equal(100m, Assert.Single(april).Amount);
    }

    /// <summary>TC11 — paying more than the month owes is a negative remainder, not an error.</summary>
    [Fact]
    public async Task Remaining_Goes_Negative_When_The_Employee_Is_Overpaid()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);
        const string Month = "2026-06";

        await SetSalaryAsync(client, employeeId, 600m);
        await AddEntryAsync(client, employeeId, "payment", 700m, Month);

        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(client, employeeId, Month);
        Assert.Equal(-100m, row.Remaining);
    }

    /// <summary>TC10 / TC12 — a month with nothing in it lists every employee at zero and returns no rows.</summary>
    [Fact]
    public async Task Untouched_Month_Lists_Everyone_At_Zero_And_Returns_No_Entries()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        HttpResponseMessage entries = await client.GetAsync($"/api/employees/{employeeId}/salary-entries?month=2030-01");
        Assert.Equal(HttpStatusCode.OK, entries.StatusCode);
        Assert.Empty((await entries.Content.ReadFromJsonAsync<List<IntegrationTestHelpers.SalaryEntryDto>>())!);

        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(client, employeeId, "2030-01");
        Assert.Equal(0m, row.PaidTotal);
        Assert.Equal(0m, row.DeductionTotal);
        Assert.Equal(row.MonthlySalary, row.Remaining);
    }

    /// <summary>
    /// TC3 / AC12 — a salary payment is money out of the drawer: it joins today's expenses and lowers
    /// expected cash by exactly its amount. (Once the day has been closed, expected cash is anchored to that
    /// close, so the payment is already inside it — the expectation follows the closing state rather than
    /// depending on which test class ran first.)
    /// </summary>
    [Fact]
    public async Task Salary_Payment_Is_Cash_Out_On_The_Dashboard()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        bool dayAlreadyClosed = await DayIsClosedAsync(client);
        IntegrationTestHelpers.DashboardDto before = await DashboardAsync(client);

        await AddEntryAsync(client, employeeId, "payment", 200m, month: null);

        IntegrationTestHelpers.DashboardDto after = await DashboardAsync(client);

        Assert.Equal(before.TodayExpenses + 200m, after.TodayExpenses);
        Assert.Equal(dayAlreadyClosed ? before.ExpectedCash : before.ExpectedCash - 200m, after.ExpectedCash);
    }

    /// <summary>
    /// TC5 — the critical rule: a deduction is charged against the employee's account only. No cash moves,
    /// so neither today's expenses nor expected cash may budge — while the deduction total does.
    /// </summary>
    [Fact]
    public async Task Deduction_Never_Touches_The_Cash_Figures()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);
        const string Month = "2026-12";

        IntegrationTestHelpers.DashboardDto before = await DashboardAsync(client);
        decimal deductedBefore = (await SummaryRowAsync(client, employeeId, Month)).DeductionTotal;

        await AddEntryAsync(client, employeeId, "deduction", 30m, Month, "Yemək");

        IntegrationTestHelpers.DashboardDto after = await DashboardAsync(client);

        Assert.Equal(before.TodayExpenses, after.TodayExpenses);
        Assert.Equal(before.ExpectedCash, after.ExpectedCash);

        // …but the employee's account did record it.
        Assert.Equal(deductedBefore + 30m, (await SummaryRowAsync(client, employeeId, Month)).DeductionTotal);
    }

    /// <summary>
    /// TC9 / AC4 — paying an earlier month's salary today: the cash left the drawer TODAY (dashboard), while
    /// the line settles the EARLIER month (summary). The two fields answer different questions.
    /// </summary>
    [Fact]
    public async Task Cash_Date_Is_Today_While_The_Accounting_Month_Can_Be_In_The_Past()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);
        const string PastMonth = "2026-07";

        IntegrationTestHelpers.DashboardDto before = await DashboardAsync(client);
        await SetSalaryAsync(client, employeeId, 600m);
        await AddEntryAsync(client, employeeId, "payment", 80m, PastMonth);
        IntegrationTestHelpers.DashboardDto after = await DashboardAsync(client);

        // The money moved today…
        Assert.Equal(before.TodayExpenses + 80m, after.TodayExpenses);

        // …but it settles the past month, and shows up nowhere else.
        Assert.Equal(80m, (await SummaryRowAsync(client, employeeId, PastMonth)).PaidTotal);
        List<IntegrationTestHelpers.SalaryEntryDto> pastEntries = await EntriesAsync(client, employeeId, PastMonth);
        IntegrationTestHelpers.SalaryEntryDto entry = Assert.Single(pastEntries);
        Assert.Equal(PastMonth, entry.Month);

        // The API returns UTC instants, but SQL Server hands `datetime2` back with Kind=Unspecified, so the
        // JSON carries no offset and `ToUniversalTime()` would re-interpret it as the machine's LOCAL time
        // and shift it by that offset. On a UTC+4 machine that turned this assertion red for the first four
        // hours of every UTC day. Stamp the kind instead of converting, and compare the instant rather than
        // the calendar day so a run that crosses midnight is not a failure either.
        DateTime entryUtc = entry.Date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entry.Date, DateTimeKind.Utc)
            : entry.Date.ToUniversalTime();

        Assert.True(
            (DateTime.UtcNow - entryUtc).Duration() < TimeSpan.FromMinutes(5),
            $"Sətrin tarixi indiki ana yaxın olmalıdır: {entryUtc:O} vs {DateTime.UtcNow:O}");
    }

    /// <summary>TC26 — the salary line and its activity entry are written together; the feed shows it.</summary>
    [Fact]
    public async Task Creating_An_Entry_Writes_An_Activity_Log()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        await AddEntryAsync(client, employeeId, "payment", 123m, month: null, note: "Avans");

        List<IntegrationTestHelpers.ActivityDto> feed =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ActivityDto>>("/api/activity?take=50"))!;

        Assert.Contains(feed, a => a.Action == "Maaş əməliyyatı" && a.Detail.Contains("123"));
    }

    /// <summary>TC17 / TC20 — deleting is owner-only, scoped to the employee in the route, and reversible in the summary.</summary>
    [Fact]
    public async Task Delete_Is_Owner_Only_And_Cannot_Reach_Another_Employees_Entry()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        HttpClient manager = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.ManagerPhone);
        Guid employeeId = await EmployeeIdAsync(owner, IntegrationTestHelpers.SecondSellerPhone);
        Guid otherId = await EmployeeIdAsync(owner, IntegrationTestHelpers.SellerPhone);
        const string Month = "2026-10";

        await SetSalaryAsync(owner, employeeId, 600m);
        IntegrationTestHelpers.SalaryEntryDto entry = await AddEntryAsync(owner, employeeId, "payment", 90m, Month);
        Assert.Equal(90m, (await SummaryRowAsync(owner, employeeId, Month)).PaidTotal);

        // A manager may record a line but not remove one — and the line survives the attempt.
        HttpResponseMessage forbidden = await manager.DeleteAsync(
            $"/api/employees/{employeeId}/salary-entries/{entry.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(90m, (await SummaryRowAsync(owner, employeeId, Month)).PaidTotal);

        // TC20: the same entry id under a different employee is simply not found — no cross-employee leak.
        HttpResponseMessage wrongOwner = await owner.DeleteAsync(
            $"/api/employees/{otherId}/salary-entries/{entry.Id}");
        Assert.Equal(HttpStatusCode.NotFound, wrongOwner.StatusCode);
        var error = (await wrongOwner.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Salary.EntryNotFound", error.Code);
        Assert.Equal(90m, (await SummaryRowAsync(owner, employeeId, Month)).PaidTotal);

        // The owner's delete goes through and the summary drops back.
        HttpResponseMessage deleted = await owner.DeleteAsync(
            $"/api/employees/{employeeId}/salary-entries/{entry.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(0m, (await SummaryRowAsync(owner, employeeId, Month)).PaidTotal);
    }

    /// <summary>TC15 / TC16 / TC18 — the whole role matrix for the five new routes.</summary>
    [Fact]
    public async Task Role_Matrix_Is_Enforced()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        HttpClient manager = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.ManagerPhone);
        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        Guid employeeId = await EmployeeIdAsync(owner, IntegrationTestHelpers.SecondSellerPhone);

        // PUT .../salary — owner only.
        Assert.Equal(HttpStatusCode.Forbidden, (await SetSalaryResponseAsync(manager, employeeId, 700m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetSalaryResponseAsync(seller, employeeId, 700m)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SetSalaryResponseAsync(owner, employeeId, 600m)).StatusCode);

        // POST .../salary-entries — owner or manager.
        Assert.Equal(HttpStatusCode.Created, (await AddEntryResponseAsync(manager, employeeId, "payment", 10m, "2026-09")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddEntryResponseAsync(seller, employeeId, "payment", 10m, "2026-09")).StatusCode);

        // GET .../salary-entries — every authenticated role.
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"/api/employees/{employeeId}/salary-entries?month=2026-09")).StatusCode);

        // GET /salary-summary — owner or manager.
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/employees/salary-summary?month=2026-09")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/employees/salary-summary?month=2026-09")).StatusCode);

        // TC25: anonymous is rejected everywhere, before any role check.
        HttpClient anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await SetSalaryResponseAsync(anonymous, employeeId, 1m)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AddEntryResponseAsync(anonymous, employeeId, "payment", 1m, "2026-09")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/employees/{employeeId}/salary-entries")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/employees/salary-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/employees/{employeeId}/salary-entries/{Guid.NewGuid()}")).StatusCode);
    }

    /// <summary>TC19 — every route rejects an employee id that does not exist with the same 404 contract.</summary>
    [Fact]
    public async Task Unknown_Employee_Is_A_404_Everywhere()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid missing = Guid.NewGuid();

        HttpResponseMessage put = await SetSalaryResponseAsync(client, missing, 600m);
        HttpResponseMessage post = await AddEntryResponseAsync(client, missing, "payment", 10m, "2026-03");
        HttpResponseMessage get = await client.GetAsync($"/api/employees/{missing}/salary-entries?month=2026-03");

        foreach (HttpResponseMessage response in new[] { put, post, get })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
            Assert.Equal("Auth.UserNotFound", error.Code);
            Assert.Equal("İstifadəçi tapılmadı", error.Message);
        }
    }

    /// <summary>TC21 / TC22 / TC23 / TC24 — every bad input is a 400 with an Azerbaijani message, never a 500.</summary>
    [Fact]
    public async Task Invalid_Input_Is_Rejected_With_A_Business_Error()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        // TC21: an unknown type has its own code.
        HttpResponseMessage badType = await AddEntryResponseAsync(client, employeeId, "bonus", 10m, null);
        await AssertBadRequestAsync(badType, "Salary.InvalidType");

        // TC22: a non-positive amount.
        await AssertBadRequestAsync(await AddEntryResponseAsync(client, employeeId, "payment", 0m, null));
        await AssertBadRequestAsync(await AddEntryResponseAsync(client, employeeId, "payment", -5m, null));

        // TC23: malformed months, in the body and in the query string.
        await AssertBadRequestAsync(await AddEntryResponseAsync(client, employeeId, "payment", 10m, "26-8"), "Salary.InvalidMonth");
        await AssertBadRequestAsync(await client.GetAsync("/api/employees/salary-summary?month=2026-13"), "Salary.InvalidMonth");
        await AssertBadRequestAsync(await client.GetAsync("/api/employees/salary-summary?month=avqust"), "Salary.InvalidMonth");
        await AssertBadRequestAsync(await client.GetAsync($"/api/employees/{employeeId}/salary-entries?month=2026-13"), "Salary.InvalidMonth");

        // TC24: a negative salary, and the stored value survives untouched.
        await SetSalaryAsync(client, employeeId, 600m);
        await AssertBadRequestAsync(await SetSalaryResponseAsync(client, employeeId, -1m));
        Assert.Equal(600m, (await EmployeesAsync(client)).Single(e => e.Id == employeeId).MonthlySalary);
    }

    /// <summary>
    /// TC27 — <c>/salary-summary</c> is a literal segment while its siblings are <c>{id:guid}</c>, so the two
    /// can never be confused for one another.
    /// </summary>
    [Fact]
    public async Task Salary_Summary_Route_Does_Not_Collide_With_The_Employee_Id_Route()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        HttpResponseMessage summary = await client.GetAsync("/api/employees/salary-summary");
        HttpResponseMessage entries = await client.GetAsync($"/api/employees/{employeeId}/salary-entries");

        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, entries.StatusCode);
        Assert.NotEmpty((await summary.Content.ReadFromJsonAsync<List<IntegrationTestHelpers.SalarySummaryDto>>())!);
    }

    // --- helpers -------------------------------------------------------------------------------------

    private static async Task<List<IntegrationTestHelpers.EmployeeDto>> EmployeesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<IntegrationTestHelpers.EmployeeDto>>("/api/employees"))!;

    private static async Task<Guid> EmployeeIdAsync(HttpClient client, string phone) =>
        (await EmployeesAsync(client)).Single(e => e.Phone == phone).Id;

    private static Task<HttpResponseMessage> SetSalaryResponseAsync(HttpClient client, Guid id, decimal monthlySalary) =>
        client.PutAsJsonAsync($"/api/employees/{id}/salary", new { monthlySalary });

    private static async Task SetSalaryAsync(HttpClient client, Guid id, decimal monthlySalary)
    {
        HttpResponseMessage response = await SetSalaryResponseAsync(client, id, monthlySalary);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> AddEntryResponseAsync(
        HttpClient client, Guid id, string type, decimal amount, string? month, string? note = null) =>
        client.PostAsJsonAsync($"/api/employees/{id}/salary-entries", new { type, amount, note, month });

    private static async Task<IntegrationTestHelpers.SalaryEntryDto> AddEntryAsync(
        HttpClient client, Guid id, string type, decimal amount, string? month, string? note = null)
    {
        HttpResponseMessage response = await AddEntryResponseAsync(client, id, type, amount, month, note);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.SalaryEntryDto>())!;
    }

    private static async Task<List<IntegrationTestHelpers.SalaryEntryDto>> EntriesAsync(
        HttpClient client, Guid id, string month) =>
        (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SalaryEntryDto>>(
            $"/api/employees/{id}/salary-entries?month={month}"))!;

    private static async Task<IntegrationTestHelpers.SalarySummaryDto> SummaryRowAsync(
        HttpClient client, Guid id, string month)
    {
        List<IntegrationTestHelpers.SalarySummaryDto> rows =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.SalarySummaryDto>>(
                $"/api/employees/salary-summary?month={month}"))!;
        return rows.Single(r => r.UserId == id);
    }

    private static async Task<IntegrationTestHelpers.DashboardDto> DashboardAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<IntegrationTestHelpers.DashboardDto>("/api/reports/dashboard"))!;

    private static async Task<bool> DayIsClosedAsync(HttpClient client)
    {
        string body = await (await client.GetAsync("/api/closings/today")).Content.ReadAsStringAsync();
        return !string.IsNullOrWhiteSpace(body) && body != "null";
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string? expectedCode = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        if (expectedCode is not null)
            Assert.Equal(expectedCode, error.Code);
    }
}
