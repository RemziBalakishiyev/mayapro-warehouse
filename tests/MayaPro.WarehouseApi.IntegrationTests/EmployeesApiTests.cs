using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    /// <summary>TC13 / TC14 / TC-21 — the employee row's wire shape after BE#57.</summary>
    [Fact]
    public async Task Employees_List_Carries_MonthlySalary_And_Position()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);

        await SetSalaryAsync(client, employeeId, 600m);

        List<IntegrationTestHelpers.EmployeeDto> all = await EmployeesAsync(client);
        IntegrationTestHelpers.EmployeeDto paid = all.Single(e => e.Id == employeeId);
        Assert.Equal(600m, paid.MonthlySalary);

        Assert.Equal(IntegrationTestHelpers.SecondSellerPhone, paid.Phone);
        Assert.True(paid.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(paid.FullName));

        // BE#57: free-text position, never a role code — an employee has no role because it has no login.
        Assert.Equal("Satıcı", paid.Position);

        // TC14: an employee nobody has set a salary for reads 0, never null.
        IntegrationTestHelpers.EmployeeDto untouched =
            all.Single(e => e.Phone == IntegrationTestHelpers.ManagerPhone);
        Assert.Equal(0m, untouched.MonthlySalary);
    }

    /// <summary>
    /// TC-21 — the breaking wire change, asserted on the raw JSON rather than on a DTO (a deserialiser would
    /// happily ignore a leftover <c>role</c> field): the employee row carries <c>position</c> and no
    /// <c>role</c>, and the salary rows are keyed by <c>employeeId</c> while still naming who recorded them.
    /// </summary>
    [Fact]
    public async Task Wire_Fields_Match_The_BE57_Contract()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid employeeId = await EmployeeIdAsync(client, IntegrationTestHelpers.SecondSellerPhone);
        await AddEntryAsync(client, employeeId, "payment", 12m, "2027-01");

        JsonElement employee = (await Json(client, "/api/employees"))
            .EnumerateArray().First(e => e.GetProperty("id").GetGuid() == employeeId);

        Assert.True(employee.TryGetProperty("position", out _));
        Assert.True(employee.TryGetProperty("isActive", out _));
        Assert.True(employee.TryGetProperty("monthlySalary", out _));
        Assert.True(employee.TryGetProperty("note", out _));
        Assert.False(employee.TryGetProperty("role", out _));

        JsonElement summary = (await Json(client, "/api/employees/salary-summary?month=2027-01"))
            .EnumerateArray().First(r => r.GetProperty("employeeId").GetGuid() == employeeId);
        Assert.True(summary.TryGetProperty("position", out _));
        Assert.False(summary.TryGetProperty("role", out _));
        Assert.False(summary.TryGetProperty("userId", out _));

        JsonElement entry = (await Json(client, $"/api/employees/{employeeId}/salary-entries?month=2027-01"))
            .EnumerateArray().First();
        Assert.Equal(employeeId, entry.GetProperty("employeeId").GetGuid());
        Assert.False(entry.TryGetProperty("userId", out _));

        // ADR-0006: the frozen type vocabulary is untouched by the rename.
        Assert.Equal("payment", entry.GetProperty("type").GetString());

        // "Who paid" survives the split and is a login account, not the employee.
        Guid recordedBy = entry.GetProperty("createdByUserId").GetGuid();
        Assert.NotEqual(Guid.Empty, recordedBy);
        Assert.NotEqual(employeeId, recordedBy);
    }

    /// <summary>
    /// TC-1 / TC-7 — putting someone on the payroll needs a name and a job title, nothing else: no password,
    /// no e-mail, no role. The new row is immediately visible in the listing and in the salary summary.
    /// </summary>
    [Fact]
    public async Task Creating_An_Employee_Needs_No_Account()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/employees", new { fullName = "Aysel Məmmədova", position = "Satıcı" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.EmployeeDto>())!;
        Assert.Equal($"/api/employees/{created.Id}", response.Headers.Location?.ToString());
        Assert.Null(created.Phone);
        Assert.Null(created.Note);
        Assert.Equal(0m, created.MonthlySalary);
        Assert.True(created.IsActive);

        Assert.Contains(await EmployeesAsync(client), e => e.Id == created.Id);

        // TC-2: agree a salary and it shows up as the month's full outstanding amount.
        await SetSalaryAsync(client, created.Id, 600m);
        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(client, created.Id, "2027-02");
        Assert.Equal(600m, row.MonthlySalary);
        Assert.Equal(0m, row.PaidTotal);
        Assert.Equal(600m, row.Remaining);
    }

    /// <summary>
    /// TC-3 — the task's headline scenario on a brand-new employee: 600 agreed, 100 + 50 paid, 30 deducted
    /// → 150 paid, 30 deducted, 420 left, and three lines newest first.
    /// </summary>
    [Fact]
    public async Task New_Employee_Salary_Account_Adds_Up()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        const string Month = "2026-03";

        Guid id = await CreateEmployeeAsync(client, "Kamran Səfərov", "Fəhlə");

        await SetSalaryAsync(client, id, 600m);
        await AddEntryAsync(client, id, "payment", 100m, Month, "Avans");
        await AddEntryAsync(client, id, "payment", 50m, Month);
        await AddEntryAsync(client, id, "deduction", 30m, Month, "Yemək");

        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(client, id, Month);
        Assert.Equal(600m, row.MonthlySalary);
        Assert.Equal(150m, row.PaidTotal);
        Assert.Equal(30m, row.DeductionTotal);
        Assert.Equal(420m, row.Remaining);

        List<IntegrationTestHelpers.SalaryEntryDto> entries = await EntriesAsync(client, id, Month);
        Assert.Equal(3, entries.Count);
        Assert.Equal("deduction", entries[0].Type);   // newest first
        Assert.All(entries, e => Assert.Equal(id, e.EmployeeId));
    }

    /// <summary>
    /// TC-4 / TC-5 / TC-19 — leaving the shop is a deactivation, never a delete. The row stays in the list
    /// with the flag down, the final settlement can still be paid, and the state statements are idempotent.
    /// </summary>
    [Fact]
    public async Task Deactivating_Keeps_The_Employee_And_Their_History()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        const string Month = "2027-03";
        Guid id = await CreateEmployeeAsync(client, "Orxan Səfərov", "Sürücü");
        await SetSalaryAsync(client, id, 500m);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/employees/{id}/deactivate", null)).StatusCode);
        Assert.False((await EmployeesAsync(client)).Single(e => e.Id == id).IsActive);

        // TC-5: a departed employee can still be settled up, and it lands in the summary.
        await AddEntryAsync(client, id, "payment", 200m, Month, "Son haqq-hesab");
        Assert.Equal(200m, (await SummaryRowAsync(client, id, Month)).PaidTotal);

        // Idempotent in both directions.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/employees/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/employees/{id}/activate", null)).StatusCode);
        Assert.True((await EmployeesAsync(client)).Single(e => e.Id == id).IsActive);

        // TC-19: there is no way to erase the record — and the history is still there afterwards.
        HttpResponseMessage deleted = await client.DeleteAsync($"/api/employees/{id}");
        Assert.True(
            deleted.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"İşçini silmək mümkün olmamalıdır, amma cavab {(int)deleted.StatusCode} gəldi");
        Assert.Contains(await EmployeesAsync(client), e => e.Id == id);
        Assert.Equal(200m, (await SummaryRowAsync(client, id, Month)).PaidTotal);
    }

    /// <summary>TC-6 — two employees may share a phone number: it is a contact, not a login identifier.</summary>
    [Fact]
    public async Task Two_Employees_May_Share_A_Phone_Number()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        Guid first = await CreateEmployeeAsync(client, "Kamran Qardaşov", "Fəhlə", phone: "0509998877");
        Guid second = await CreateEmployeeAsync(client, "Orxan Qardaşov", "Fəhlə", phone: "+994 50 999 88 77");

        List<IntegrationTestHelpers.EmployeeDto> all = await EmployeesAsync(client);
        Assert.Equal("994509998877", all.Single(e => e.Id == first).Phone);
        Assert.Equal("994509998877", all.Single(e => e.Id == second).Phone);
    }

    /// <summary>TC-8(b) / TC-10 — bad input on the new routes is a 400 in Azerbaijani, never a 500.</summary>
    [Fact]
    public async Task Employee_Validation_Is_Enforced()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        await AssertBadRequestAsync(await CreateEmployeeResponseAsync(client, "", "Satıcı"));
        await AssertBadRequestAsync(await CreateEmployeeResponseAsync(client, "Aysel", ""));
        await AssertBadRequestAsync(await CreateEmployeeResponseAsync(client, "Aysel", "Satıcı", monthlySalary: -5m));
        await AssertBadRequestAsync(await CreateEmployeeResponseAsync(client, "Aysel", "Satıcı", note: new string('a', 501)));
        await AssertBadRequestAsync(await CreateEmployeeResponseAsync(client, "Aysel", "Satıcı", phone: "abc"));
    }

    /// <summary>
    /// TC-9 — maintaining the payroll is owner/manager work; a seller may look but not touch. The employee
    /// carries a real agreed salary throughout (BE#59): on a zero-salary employee an edit that quietly resets
    /// the figure is indistinguishable from one that leaves it alone, which is precisely how the wipe survived
    /// this test the first time round.
    /// </summary>
    [Fact]
    public async Task Payroll_Maintenance_Is_Owner_Or_Manager_Only()
    {
        HttpClient owner = await _factory.AuthenticatedClientAsync();
        HttpClient manager = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.ManagerPhone);
        HttpClient seller = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);
        const string Month = "2027-05";

        Guid id = await CreateEmployeeAsync(owner, "Rüfət Nəsirov", "Fəhlə");
        await SetSalaryAsync(owner, id, 600m);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateEmployeeResponseAsync(seller, "Kim", "Satıcı")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateEmployeeResponseAsync(seller, id, "Kim", "Satıcı")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsync($"/api/employees/{id}/deactivate", null)).StatusCode);

        // …but reading the register is open to every signed-in role.
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync("/api/employees")).StatusCode);

        // A manager may do all three.
        Assert.Equal(HttpStatusCode.Created, (await CreateEmployeeResponseAsync(manager, "Səbinə Rəhimli", "Satıcı")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UpdateEmployeeResponseAsync(manager, id, "Rüfət Nəsirov", "Sürücü")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/api/employees/{id}/activate", null)).StatusCode);

        // BE#59: …and the promotion changed the job title only. The agreed salary survived an edit body that
        // never mentioned it, so the month still owes the full 600 instead of reading −0 against a wiped figure.
        Assert.Equal(600m, await MonthlySalaryAsync(owner, id));
        IntegrationTestHelpers.SalarySummaryDto row = await SummaryRowAsync(owner, id, Month);
        Assert.Equal(600m, row.MonthlySalary);
        Assert.Equal(600m, row.Remaining);

        // TC-25: anonymous is rejected before any role check.
        HttpClient anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateEmployeeResponseAsync(anonymous, "Kim", "Satıcı")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/employees/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/employees")).StatusCode);
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

        // BE#59 — the 403 above is only worth something if the wider edit route cannot deliver the same
        // outcome. PUT /{id} is OwnerOrManager, so a manager may edit the register (200), but the agreed
        // salary is not part of what an edit reaches: not by sending a figure, not by omitting the field.
        // Asserted on an employee of this test's own, so renaming it cannot disturb the shared fixtures.
        Guid paid = await CreateEmployeeAsync(owner, "Nurlan Əliyev", "Fəhlə");
        await SetSalaryAsync(owner, paid, 600m);

        Assert.Equal(
            HttpStatusCode.OK,
            (await UpdateEmployeeResponseAsync(manager, paid, "Nurlan Əliyev", "Sürücü", monthlySalary: 5000m)).StatusCode);
        Assert.Equal(600m, await MonthlySalaryAsync(owner, paid));

        Assert.Equal(
            HttpStatusCode.OK,
            (await UpdateEmployeeResponseAsync(manager, paid, "Nurlan Əliyev", "Sürücü")).StatusCode);
        Assert.Equal(600m, await MonthlySalaryAsync(owner, paid));

        // The owner's own edit is no exception — the route is the rule, not the role using it.
        Assert.Equal(
            HttpStatusCode.OK,
            (await UpdateEmployeeResponseAsync(owner, paid, "Nurlan Əliyev", "Fəhlə", monthlySalary: 5000m)).StatusCode);
        Assert.Equal(600m, await MonthlySalaryAsync(owner, paid));

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

    /// <summary>
    /// TC-11 — every route rejects an employee id that does not exist with the same 404 contract. BE#57
    /// changed which contract that is: the salary routes talk about an employee ("İşçi tapılmadı"), not
    /// about a login account, because after the split they never look at accounts at all.
    /// </summary>
    [Fact]
    public async Task Unknown_Employee_Is_A_404_Everywhere()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        Guid missing = Guid.NewGuid();

        HttpResponseMessage put = await SetSalaryResponseAsync(client, missing, 600m);
        HttpResponseMessage post = await AddEntryResponseAsync(client, missing, "payment", 10m, "2026-03");
        HttpResponseMessage get = await client.GetAsync($"/api/employees/{missing}/salary-entries?month=2026-03");
        HttpResponseMessage edit = await UpdateEmployeeResponseAsync(client, missing, "Aysel", "Satıcı");
        HttpResponseMessage off = await client.PostAsync($"/api/employees/{missing}/deactivate", null);
        HttpResponseMessage on = await client.PostAsync($"/api/employees/{missing}/activate", null);

        foreach (HttpResponseMessage response in new[] { put, post, get, edit, off, on })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
            Assert.Equal("Employee.NotFound", error.Code);
            Assert.Equal("İşçi tapılmadı", error.Message);
        }
    }

    /// <summary>
    /// BE#57 — the two registers really are separate over the wire: an <b>account</b> id is not an employee
    /// id, so the salary routes turn one down exactly as they turn down a random Guid.
    /// </summary>
    [Fact]
    public async Task A_Login_Account_Id_Is_Not_An_Employee_Id()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        var me = (await client.GetFromJsonAsync<MeDto>("/api/auth/me"))!;

        Assert.DoesNotContain(await EmployeesAsync(client), e => e.Id == me.Id);

        HttpResponseMessage response = await AddEntryResponseAsync(client, me.Id, "payment", 10m, "2026-03");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.ErrorDto>())!;
        Assert.Equal("Employee.NotFound", error.Code);
    }

    private sealed record MeDto(Guid Id, string FullName, string Role);

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

    /// <summary>Reads a response as raw JSON — the only way to assert that a field is <b>absent</b>.</summary>
    private static async Task<JsonElement> Json(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static Task<HttpResponseMessage> CreateEmployeeResponseAsync(
        HttpClient client, string fullName, string position, string? phone = null,
        decimal monthlySalary = 0m, string? note = null) =>
        client.PostAsJsonAsync("/api/employees", new { fullName, phone, position, monthlySalary, note });

    private static async Task<Guid> CreateEmployeeAsync(
        HttpClient client, string fullName, string position, string? phone = null, decimal monthlySalary = 0m)
    {
        HttpResponseMessage response = await CreateEmployeeResponseAsync(
            client, fullName, position, phone, monthlySalary);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IntegrationTestHelpers.EmployeeDto>())!.Id;
    }

    /// <summary>
    /// The edit body carries a <c>monthlySalary</c> only when a test deliberately sends one (BE#59): the field
    /// is not part of the contract, and a helper that always attached it would hide the very case that broke —
    /// the natural "edit the name and the job title" body, which sends nothing else.
    /// </summary>
    private static Task<HttpResponseMessage> UpdateEmployeeResponseAsync(
        HttpClient client, Guid id, string fullName, string position, string? phone = null,
        decimal? monthlySalary = null, string? note = null) =>
        monthlySalary is null
            ? client.PutAsJsonAsync($"/api/employees/{id}", new { fullName, phone, position, note })
            : client.PutAsJsonAsync($"/api/employees/{id}", new { fullName, phone, position, note, monthlySalary });

    private static async Task<decimal> MonthlySalaryAsync(HttpClient client, Guid id) =>
        (await EmployeesAsync(client)).Single(e => e.Id == id).MonthlySalary;

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
        return rows.Single(r => r.EmployeeId == id);
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
