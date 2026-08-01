# QA Report — BE#28: İşçi maaş sistemi (MonthlySalary, SalaryEntry, gün sonu inteqrasiyası)

**Tarix:** 2026-08-02
**QA Agent:** qa-tester
**Test edilən PR:** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/29 (branch `task/BE28-employee-salary`, commit `f05fc33`)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/28
**Mühit:** Lokal, Windows 11, .NET 8 SDK, SQL Server (`MayaProWarehouse_Test`), `dotnet build` / `dotnet test` (tam solution, 11 test layihəsi)

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Acceptance Criteria | 16 (AC1–AC16) — 16 ✅ / 0 ❌ |
| Test case | 30 (TC1–TC30) — 30 ✅ / 0 ❌ / 0 ⚠️ |
| Yaradılan bug sayı | **0** |
| Test örtüyü GAP-ları | 2 (GAP-1: TC26 rollback simulyasiyası, GAP-2: kontraktın diapazon yolu SQL üzərində sınanmayıb) |
| Qeydlər (bloklamayan) | 5 (OBS-1…OBS-5) |
| **Yekun qərar** | **QA PASSED → Done** |

## Build / Test rəqəmləri (bu QA sessiyasında bilavasitə icra edilib)

```
dotnet build MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be28/
# → Build succeeded.
#     0 Warning(s)
#     0 Error(s)
#   Time Elapsed 00:00:14.53
```

```
dotnet test MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be28/
# Passed! - Failed: 0, Passed:  36, Total:  36 - SharedKernel.Tests.dll
# Passed! - Failed: 0, Passed:  12, Total:  12 - Modules.Suppliers.Tests.dll
# Passed! - Failed: 0, Passed:  54, Total:  54 - Modules.Expenses.Tests.dll
# Passed! - Failed: 0, Passed:  25, Total:  25 - Modules.Reports.Tests.dll
# Passed! - Failed: 0, Passed:   4, Total:   4 - Modules.DayEnd.Tests.dll
# Passed! - Failed: 0, Passed:  16, Total:  16 - Modules.Customers.Tests.dll
# Passed! - Failed: 0, Passed:  50, Total:  50 - Modules.Sales.Tests.dll
# Passed! - Failed: 0, Passed:  46, Total:  46 - Modules.Exports.Tests.dll
# Passed! - Failed: 0, Passed:  71, Total:  71 - Modules.Products.Tests.dll
# Passed! - Failed: 0, Passed:  50, Total:  50 - Modules.Auth.Tests.dll
# Passed! - Failed: 0, Passed: 180, Total: 180 - IntegrationTests.dll (33 s)
```

- **Cəm: 544/544 yaşıl, 0 uğursuz, 0 skipped.** Tam paket bir dəfəyə (filtersiz) işlədilib — senior-backend-in `GetTodayClosingHandler` düzəlişindən (`f05fc33`) sonra `EmployeesApiTests.Salary_Payment_Is_Cash_Out_On_The_Dashboard` artıq sabit keçir, təkrar icrada da fail yoxdur.
- BE#21 QA sessiyasındakı baza 479 idi → bu PR **+65 test** gətirir: Auth 4→50 (`SalaryEntryHandlerTests`, `GetSalarySummaryHandlerTests`, `SalaryModuleContractTests`, `SalaryWireFormatTests`), Reports 22→25, Expenses 52→54, IntegrationTests 166→180 (`EmployeesApiTests` 13 test + `DayEndApiTests` genişlənməsi).
- İnteqrasiya testləri real host + real SQL Server üzərində işləyir → migration, `identity.SalaryEntries` cədvəli, paylaşılan transaction və auth pipeline faktiki olaraq yoxlanılıb.

### QA-nın öz müstəqil probe-ları (bu sessiyada yazılıb, icra olunub və sonra silinib)

Commit olunmuş testlərin HTTP səviyyəsində iddia etmədiyi davranışlar üçün müvəqqəti iki probe sinfi (`QaBe28ProbeTests` — 8 test, `QaBe28CashProbeTests` — 1 uzun ssenari) real host + real SQL Server üzərində işlədildi. **9/9 yaşıl.** Fayllar QA-dan sonra silindi (PR-a daxil deyil), nəticələr aşağıdakı cədvəllərdə istinad edilir.

```
Passed QaBe28ProbeTests.Probe_Created_Response_Shape                       [393 ms]
Passed QaBe28ProbeTests.Probe_Entries_Are_Newest_First                     [3 s]
Passed QaBe28ProbeTests.Probe_Note_Longer_Than_500_Is_A_400                [401 ms]
Passed QaBe28ProbeTests.Probe_Summary_Lists_Everyone_And_Defaults_To_This_Month [429 ms]
Passed QaBe28ProbeTests.Probe_Seller_Sees_Everyones_Salary                 [747 ms]
Passed QaBe28ProbeTests.Probe_Zero_Salary_And_Deduction_Only_Month         [148 ms]
Passed QaBe28ProbeTests.Probe_Regression_Of_Existing_Flows                 [1 s]
Passed QaBe28ProbeTests.Probe_Route_Shapes                                 [139 ms]
Passed QaBe28CashProbeTests.Probe_Payment_And_Deduction_Through_Dashboard_And_Closing [3 s]
```

Kassa probe-unun ölçdüyü dəqiq rəqəmlər (sıfırlanmış baza, bağlanmamış gün):
30 AZN nağd satış + 40 AZN mağaza xərci → 200 AZN maaş ödənişi → 30 AZN tutulma → gün bağlanışı.

| Ölçü | Gözlənilən | Faktiki |
|---|---|---|
| `todayExpenses` ödənişdən sonra | +200 | +200 |
| `expectedCash` ödənişdən sonra | −200 | −200 |
| `todayExpenses` / `expectedCash` tutulmadan sonra | dəyişmir | dəyişmədi |
| `Closing.Expenses` | 240 (40 + 200, 30 YOX) | **240** |
| `Closing.Expenses` == dashboard `todayExpenses` | bərabər | bərabər |
| `Closing.ExpectedCash` | 100 + 30 − 240 = −110 | **−110** |
| `Closing.Difference` | `actualCash − expectedCash` | doğru |
| Bağlanışdan sonra `expectedCash` | 0 (`actualCash`-a bağlanır) | 0 |
| Bağlanışdan sonra yeni 50 AZN ödəniş | `todayExpenses` +50, `expectedCash` dəyişmir | doğru (ikiqat çıxılma yoxdur) |

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Faktiki yoxlama |
|---|---|---|---|
| AC1 | `User.MonthlySalary` + migration | ✅ | Migration `20260801193834_EmployeeSalaryAndSalaryEntries` — `MonthlySalary decimal(18,2) NOT NULL DEFAULT 0` (`Up`: `AddColumn<decimal>(… defaultValue: 0m)`). `User`-də public setter yoxdur, dəyişiklik yalnız `SetMonthlySalary(decimal)` ilə (`Activate()/Deactivate()` üslubu). Real SQL Server-də migration işləyir (bütün 180 inteqrasiya testi baza sıfırlanıb migrate ediləndən sonra keçir); toxunulmamış işçi `monthlySalary: 0` qaytarır (QA probe: seed Menecer sətri). |
| AC2 | `PUT /{id}/salary` (OwnerOnly) | ✅ | `EmployeesEndpoints.cs:41-48` — `.RequireAuthorization(OwnerOnly)`. 200 + yenilənmiş `EmployeeDto`; `GET /api/employees`-də dərhal əks olunur (`Employees_List_Carries_MonthlySalary_And_Defaults_To_Zero`, QA probe `Probe_Seller_Sees_Everyones_Salary` — 777 dəyəri). `monthlySalary = -1` → 400 «Maaş mənfi ola bilməz», saxlanılmış dəyər dəyişmir. Naməlum id → 404 `Auth.UserNotFound`. `monthlySalary = 0` qəbul olunur (QA probe). |
| AC3 | `SalaryEntry` + `POST .../salary-entries` (O+M) | ✅ | **QA probe HTTP səviyyəsində birbaşa yoxladı:** 201 Created, `Location: /api/employees/{id}/salary-entries/{entryId}` (dəqiq bərabərlik), gövdədə 9 sahənin hamısı (`id, userId, type, amount, note, date, month, createdByUserId, createdAt`), `createdByUserId` == `GET /api/auth/me`-dəki id. `month` göndərilməyəndə `dateProvider.Today`-in `yyyy-MM` forması (probe: cari ay), `Date` = `dateProvider.UtcNow`. `identity.SalaryEntries` sətri real SQL Server-də yaranır. |
| AC4 | `Date` ≠ `Month` semantikası | ✅ | `Cash_Date_Is_Today_While_The_Accounting_Month_Can_Be_In_The_Past` (e2e): `month = "2026-07"` sətri bugünkü `todayExpenses`-i 80 artırır, `paidTotal` isə iyulun xülasəsində görünür; `entry.Date` bugünkü UTC tarixidir. Unit: `Create_Keeps_Cash_Date_And_Accounting_Month_Independent`. Kod tərəfdə: gün sonu/dashboard `Date` üzrə (`LocalDayRangeUtc`), xülasə/siyahı `Month` üzrə filtrləyir. |
| AC5 | Activity log, eyni transaksiyada | ✅ | `CreateSalaryEntryHandler.cs:60-71` — `BeginTransactionAsync` → `SaveChangesAsync` → `CommitAsync` (`CreateExpenseHandler` üslubu); `AuthDbContext` `ITransactionalDbContext` kimi paylaşılan bağlantıdadır, ona görə sətir və log fiziki olaraq bir transaksiyadadır. e2e: `Creating_An_Entry_Writes_An_Activity_Log` — feed-də `action = "Maaş əməliyyatı"`, `detail` işçinin adı + məbləğ + növ («Günel Quliyeva — 123.00 AZN ödəniş verildi»). Unit: `Assert.True(uow.Committed)` + tək log sətri. Bax OBS-3, GAP-1. |
| AC6 | `GET .../salary-entries?month=` | ✅ | **QA probe:** eyni ayda 3 sətir → cavab `Date` DESC (ən yenisi birinci, `rows[0].Amount == 30`). `month` verilmədikdə cari ay; `?month=2026-13` → 400 `Salary.InvalidMonth` (`TryParseExact`, `InvariantCulture`); naməlum işçi → 404 `Auth.UserNotFound`; sətri olmayan ay → 200 + boş massiv (`Untouched_Month_Lists_Everyone_At_Zero_And_Returns_No_Entries`). |
| AC7 | `DELETE .../salary-entries/{entryId}` (OwnerOnly) | ✅ | `Delete_Is_Owner_Only_And_Cannot_Reach_Another_Employees_Entry`: Manager → 403 (sətir qalır, `paidTotal` 90 olaraq dəyişməz), yad işçinin route-u → 404 `Salary.EntryNotFound` (sətir yenə qalır), Owner → 200 və `paidTotal` 90 → 0. `DeleteSalaryEntryHandler` sətri `Id` VƏ `UserId` ilə birgə axtarır. Activity log silinmə üçün də yazılır (unit `Delete_Removes_The_Line_And_Logs_It`, `uow.Committed`). |
| AC8 | `GET /salary-summary?month=` (O+M) | ✅ | 7 sahənin hamısı qaytarılır. **QA probe:** cavab sətirlərinin sayı `GET /api/employees` ilə eynidir (4 seed işçi), hər sətirdə `fullName` boş deyil və `role` `sahib/menecer/satici` lüğətindəndir; `month` verilmədikdə cari ay (probe 15 AZN-lik ödənişin default aya düşdüyünü təsdiqlədi). `paidTotal`/`deductionTotal` növ üzrə ayrılır, sətri olmayan işçi `0/0/monthlySalary` ilə görünür; format səhvi → 400. |
| AC9 | Mənfi qalıq | ✅ | e2e `Remaining_Goes_Negative_When_The_Employee_Is_Overpaid` (600 maaş, 700 ödəniş → `-100`, 200 OK). QA probe əlavə hal: `monthlySalary = 0` + yalnız 25.55 tutulma → `remaining = -25.55` (onluq kəsr dəqiq, kəsilmə/yuvarlaqlaşdırma yoxdur). |
| AC10 | `ISalaryModule` + gün sonu | ✅ | Kontrakt `IExpensesModule` ilə simmetrikdir (`GetDayPaymentsTotalAsync` + `GetPaymentsAsync`, `record SalaryPaymentRow`). `CloseDayHandler.cs:48,56` — `expenseTotal + salaryPaid` `Closing.Create`-ə ötürülür. **QA probe dəqiq arifmetikanı ölçdü:** `Expenses = 240`, `ExpectedCash = 100 + 30 − 240 = −110`, `Difference = ActualCash − ExpectedCash`. `ClosingDto` faylı bu PR-da toxunulmayıb → wire format dəyişməyib (ADR-0006). |
| AC11 | Deduction kassaya toxunmur | ✅ | Kontraktın hər iki metodu `Where(e => e.Type == SalaryEntryType.Payment)` ilə başlayır. Unit: `Deductions_Alone_Never_Reach_The_Cash_Figures`, `Day_Total_Sums_Only_That_Days_Payments`. e2e: `Deduction_Never_Touches_The_Cash_Figures` + `DayEndApiTests` (30 AZN tutulma `Closing.Expenses`-ə düşmür). QA probe: tutulmadan sonra `todayExpenses` və `expectedCash` bayt-bayt dəyişməz, bağlanışın `Expenses`-i 240 (270 deyil). |
| AC12 | Dashboard inteqrasiyası | ✅ | `DashboardCalculator.cs:49-50` — `todayExpenses`-ə bugünkü ödənişlər əlavə olunur; `:105-107` — `expectedCash`-də ödənişlər xərclərlə **eyni «son bağlanışdan bəri» pəncərəsindən** çıxılır. `GetDashboardHandler.cs:35` — `salary.GetPaymentsAsync(null, null, ct)`, kalkulyator saf qalır. QA probe bağlanışdan sonra yeni ödənişin `expectedCash`-i dəyişmədiyini, `todayExpenses`-i isə artırdığını təsdiqlədi. |
| AC13 | Non-regression | ✅ | Build 0 Warning / 0 Error; 544/544 test yaşıl. **QA regression probe (real host):** login (200) + səhv şifrə (400) + `GET /api/auth/me` (200) + `/api/employees` (200) + `/api/reports/dashboard` (200) + `/api/expenses` (200) + `/api/activity` (200) + `/api/products` (200) + `/api/customers` (200); `UserSeeder` yenə də **4** demo istifadəçi yaradır. Startup migration + seed `AuthDbContext`-in paylaşılan bağlantıya keçməsindən sonra da işləyir. Maaş sətri olmayan halda kalkulyator nəticəsi dəyişmir (`No_Salary_Rows_Leave_Today_Expenses_And_Expected_Cash_Untouched`). |
| AC14 | İcazə matrisi | ✅ | `Role_Matrix_Is_Enforced` 6 sətrin hamısını örtür (Owner/Manager/Seller/anonim). QA probe əlavə olaraq Seller-in `GET /api/employees`-i (200) və `GET .../salary-entries`-i (200) oxuduğunu, `salary-summary`-də isə 403 aldığını təsdiqlədi. Host-a yeni policy əlavə edilməyib — `OwnerOnly`/`OwnerOrManager` endpoint faylında lokal sabitdir (`ExpensesEndpoints` üslubu). |
| AC15 | Validasiya | ✅ | `amount = 0` və `-5` → 400 «Məbləğ sıfırdan böyük olmalıdır»; `type = "bonus"` → 400 `Salary.InvalidType`; `month = "26-8" / "2026-13" / "avqust"` → 400 `Salary.InvalidMonth`; `monthlySalary = -1` → 400. **QA probe `note` sərhədini ayrıca yoxladı:** 501 simvol → **400** (baza xətası deyil), tam 500 simvol → **201**. Sınanan heç bir halda 500 qaytarılmadı. |
| AC16 | Route toqquşması | ✅ | `salary-summary` literal seqmenti `{id:guid}` şablonlarından əvvəl gəlir və `:guid` constraint-i onu qoruyur. e2e `Salary_Summary_Route_Does_Not_Collide_With_The_Employee_Id_Route`. **QA probe əlavə hallar:** `/api/employees/not-a-guid/salary-entries` → 404 (500 deyil), `/api/employees/salary-summary/salary-entries` → 404. |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Sübut |
|---|---|---|---|
| TC1 | Qalıq hesabı: 600 / 100+50 / 30 → **420** | ✅ | e2e `EmployeesApiTests.Salary_Summary_Computes_Paid_Deducted_And_Remaining` — `monthlySalary 600`, `paidTotal 150`, `deductionTotal 30`, `remaining 420` (ay `2026-03`, izolyasiya edilmiş işçi). **QA kassa probe-u eyni ssenarini müstəqil təkrarladı və eyni dörd rəqəmi aldı.** |
| TC2 | Xülasə məntiqi (unit) | ✅ | `GetSalarySummaryHandlerTests.Remaining_Is_Salary_Minus_Payments_Minus_Deductions` — 150 / 30 / 420; filtr `Month` üzərindədir (`Months_Are_Not_Mixed`, `Totals_Are_Per_Employee`). |
| TC3 | Gün sonu `expectedCash`-a ödəniş daxil olur | ✅ | e2e `Salary_Payment_Is_Cash_Out_On_The_Dashboard` (delta ölçmə, bağlanış vəziyyətinə uyğunlaşır). **QA probe bağlanmamış gündə dəqiq bərabərliyi ölçdü:** `todayExpenses +200`, `expectedCash −200`. |
| TC4 | Bağlanışda ödəniş `Expenses`-ə düşür | ✅ | `DayEndApiTests`: `Expenses >= 240` VƏ `Assert.Equal(expensesWithSalary, c.Expenses)`. **QA probe sıfırlanmış bazada mütləq rəqəmləri təsdiqlədi:** `Expenses = 240`, `ExpectedCash = −110 = 100 + 30 − 240`, `Difference = ActualCash − ExpectedCash`. |
| TC5 | **Deduction kassaya toxunmur** | ✅ | e2e `Deduction_Never_Touches_The_Cash_Figures` — `todayExpenses` və `expectedCash` dəyişməz, `deductionTotal` +30. `DayEndApiTests` 30 AZN tutulmanın bağlanışın `Expenses`-inə düşmədiyini göstərir. QA probe: tutulmadan sonra hər iki kassa rəqəmi eyni qaldı, bağlanışın `Expenses`-i 240 (270 deyil). |
| TC6 | Kontrakt yalnız payment toplayır | ✅ | `SalaryModuleContractTests.Day_Total_Sums_Only_That_Days_Payments` — 100 + 50 payment + 30 deduction (eyni gün) + 999 payment (başqa gün) → **150** (nə 180, nə 1149). |
| TC7 | Gün sərhədi (Bakı vaxtı) | ✅ | `Day_Boundary_Follows_The_Business_Time_Zone_Not_Utc` — sətir `2026-08-01T20:30Z`; `GetDayPaymentsTotalAsync(2026-08-02) = 70`, `(2026-08-01) = 0`. Test fake deyil, **real `AppDateProvider` + `Asia/Baku` TimeZoneInfo** ilə işləyir. |
| TC8 | `month` filtri ayları qarışdırmır | ✅ | e2e `Months_Are_Kept_Apart` — `2026-04` üçün 100, `2026-05` üçün 250; hər ay öz rəqəmini qaytarır, sətir siyahısı da uyğun (aprel: tək 100-lük sətir). Unit: `Months_Are_Not_Mixed`. (PM-in nümunə ayları 03/04 idi; testdə 04/05 — ssenari eynidir, paylaşılan bazada ay izolyasiyasına görə.) |
| TC9 | `Date` bu gün, `Month` keçmiş ay | ✅ | `Cash_Date_Is_Today_While_The_Accounting_Month_Can_Be_In_The_Past` — `todayExpenses` **+80**, `paidTotal` keçən ayın (`2026-07`) xülasəsində, `entry.Date` = bugünkü UTC tarixi, `entry.Month = "2026-07"`. Bax OBS-5. |
| TC10 | Sətri olmayan işçi xülasədə var | ✅ | e2e `Untouched_Month_Lists_Everyone_At_Zero_And_Returns_No_Entries` (`0/0/monthlySalary`) + unit `Employee_Without_Entries_Is_Listed_With_Zero_Totals`. QA probe: xülasə sətirlərinin sayı işçi sayına (4) bərabərdir. |
| TC11 | Mənfi qalıq | ✅ | e2e `Remaining_Goes_Negative_When_The_Employee_Is_Overpaid` → `-100`, 200 OK. Unit `Remaining_May_Be_Negative_When_Overpaid`. QA probe: `0 − 0 − 25.55 = -25.55`. |
| TC12 | Boş nəticə | ✅ | `?month=2030-01` → **200** + boş massiv (404 yox, null yox). Unit `Entries_For_An_Empty_Month_Are_An_Empty_List`. |
| TC13 | `GET /api/employees`-də `monthlySalary` | ✅ | `Employees_List_Carries_MonthlySalary_And_Defaults_To_Zero` — 600 dəyəri görünür, mövcud sahələr (`phone`, `role`, `isActive`, `fullName`) dəyişməyib → additiv. |
| TC14 | Maaşı təyin edilməmiş işçi | ✅ | Eyni testdə: toxunulmamış Menecer sətri `monthlySalary: 0` (null yox). Migration-ın `defaultValue: 0m`-i real bazada təsdiqləndi. |
| TC15 | `PUT .../salary` — OwnerOnly | ✅ | `Role_Matrix_Is_Enforced`: Manager → 403, Seller → 403, Owner → 200. |
| TC16 | `POST .../salary-entries` — OwnerOrManager | ✅ | Manager → **201**, Seller → **403**. |
| TC17 | `DELETE` — OwnerOnly | ✅ | Manager → 403 və sətir bazada qalır (`paidTotal` 90 dəyişməz); Owner → 200, `paidTotal` 0-a düşür. |
| TC18 | `salary-summary` — OwnerOrManager | ✅ | Seller → 403, Manager → 200, anonim → 401. |
| TC19 | Mövcud olmayan işçi | ✅ | `Unknown_Employee_Is_A_404_Everywhere` — `PUT`, `POST`, `GET` üçün 404 + `{code: "Auth.UserNotFound", message: "İstifadəçi tapılmadı"}` (dəqiq mesaj yoxlanılıb). Unit səviyyəsində üç handler üçün ayrıca testlər. |
| TC20 | Yad sətrin silinməsi | ✅ | Owner tokeni ilə `DELETE /api/employees/{B}/salary-entries/{A-nın sətri}` → **404 `Salary.EntryNotFound`**, sətir silinmir (`paidTotal` 90 qalır). Unit `Delete_Through_Another_Employees_Route_Is_Not_Found`. |
| TC21 | Yanlış `type` | ✅ | `{type: "bonus"}` → 400 + `Salary.InvalidType` + «Maaş əməliyyatının növü yanlışdır»; sətir yaranmır (unit `Create_With_Unknown_Type_Is_Rejected` bazanın boş qaldığını yoxlayır). |
| TC22 | Yanlış məbləğ | ✅ | `amount = 0` və `-5` → hər ikisi 400 + Azərbaycanca mesaj; sətir yaranmır. |
| TC23 | Yanlış `month` formatı | ✅ | `?month=2026-13`, `?month=avqust`, gövdədə `month:"26-8"`, həmçinin `salary-entries?month=2026-13` → hamısı 400 + `Salary.InvalidMonth`. Səssiz iqnor və 500 yoxdur. |
| TC24 | Mənfi `monthlySalary` | ✅ | `-1` → 400 «Maaş mənfi ola bilməz»; sonrakı `GET /api/employees` saxlanılmış 600 dəyərini göstərir (dəyişməyib). |
| TC25 | Anonim giriş | ✅ | Beş yeni endpoint-in hamısı token olmadan **401** (`Role_Matrix_Is_Enforced` sonu). |
| TC26 | Activity log atomikliyi | ✅ | e2e: `/api/activity?take=50` feed-ində `action = "Maaş əməliyyatı"`, `detail`-də məbləğ və işçi adı. Kod: sətir + log eyni `IUnitOfWorkTransaction` daxilində, `AuthDbContext` artıq paylaşılan bağlantıdadır (RISK-1 həlli) → xəta halında commit baş vermir, ikisi də geri qayıdır. Xəta simulyasiyasının **commit olunmuş testi yoxdur** → GAP-1 (davranış koddan aydındır, bug deyil). |
| TC27 | Route toqquşması | ✅ | `GET /api/employees/salary-summary` → 200 (boş olmayan siyahı), `GET /api/employees/{guid}/salary-entries` → 200. QA probe: `not-a-guid` və `salary-summary/salary-entries` → 404, 400/500 yox. |
| TC28 | Mövcud axınlar sağlamdır | ✅ | Build 0/0; 544/544 test; QA regression probe-u 9 endpoint + login + seed sayını (4 istifadəçi) real host üzərində təsdiqlədi. `AuthDbContext`-in paylaşılan bağlantıya keçməsi login/seed/migration axınını pozmayıb. |
| TC29 | Maaş sətri yoxdursa dəyişiklik sıfırdır | ✅ | `SalaryModuleContractTests.Empty_Table_Yields_Zero_And_No_Rows` (0 və boş siyahı) + `DashboardCalculatorTests.No_Salary_Rows_Leave_Today_Expenses_And_Expected_Cash_Untouched` (BE#28-dən əvvəlki rəqəmlərlə eyni). |
| TC30 | Dashboard son bağlanışdan sonrakını çıxır | ✅ | `DashboardCalculatorTests.ExpectedCash_Subtracts_Only_Salary_Payments_Made_Since_The_Last_Close` — dünənki 100 çıxılmır, bugünkü 200 çıxılır. **QA probe real host-da eyni invariantı ölçdü:** bağlanışdan sonra əlavə edilən 50 AZN `expectedCash`-i dəyişmədi, `todayExpenses`-i isə artırdı. |

**Yekun: 30 ✅ / 0 ❌ / 0 ⚠️.**

## Test örtüyü GAP-ları (bug deyil)

- **GAP-1 — TC26-nın xəta simulyasiyası.** Uğurlu axın (sətir + log + commit) həm unit, həm e2e səviyyədə örtülüb, lakin «handler xəta versə nə sətir, nə log qalır» hissəsi üçün commit olunmuş test yoxdur — `FakeUnitOfWork` yalnız `Committed` bayrağını izləyir, rollback yolu simulyasiya edilmir. Davranış koddan birmənalıdır (`CreateSalaryEntryHandler.cs:60-71` — `SaveChangesAsync` yalnız transaksiya daxilində, `CommitAsync` sonda), atomikliyin fiziki şərti (paylaşılan bağlantı + `ITransactionalDbContext`) isə QA tərəfindən qeydiyyat kodunda təsdiqlənib. Gələcək task üçün kiçik iş.
- **GAP-2 — `ISalaryModule.GetPaymentsAsync(from, to)` diapazon yolu.** Produksiyada bu metod yalnız `(null, null)` ilə çağırılır (`GetDashboardHandler.cs:35`), ona görə `from`/`to` filtrləri heç vaxt relational provider (SQL Server) üzərində icra olunmur — yalnız InMemory unit testlərində (`Range_Bounds_Are_Inclusive_Days`). İfadə (`dateProvider.LocalDayRangeUtc(f).StartUtc`) sorğu kökündən asılı olmadığı üçün EF onu parametrləşdirir, yəni tərcümə problemi gözlənilmir; risk aşağıdır, amma SQL səviyyəsində sübut yoxdur. Metod gələcəkdə hesabatlarda diapazonla istifadə olunanda inteqrasiya testi əlavə edilməlidir.

## Müşahidələr (bloklamayan)

- **OBS-1 — Satıcı hamının maaşını görür.** `GET /api/employees` bütün autentifikasiya olunmuş rollara açıqdır, `monthlySalary` isə additiv sahədir. **QA probe bunu faktiki olaraq təsdiqlədi:** Satıcı tokeni ilə başqa işçinin `monthlySalary = 777` dəyəri oxunur. PM-in QEYD-1-inə uyğun olaraq bu bug kimi deyil, **müşahidə** kimi qeyd olunur; maskalama üçün ayrıca task açıla bilər.
- **OBS-2 — `PUT /{id}/salary` activity log yazmır.** Maaş sətrinin yaradılması və silinməsi log yazır, aylıq maaşın təyini isə yazmır (`SetEmployeeSalaryHandler` `IActivityLogger` almır). AC2 bunu tələb etmir, ona görə uyğunsuzluq deyil — amma sahibin həssas əməliyyatı kimi audit izi olmaması gələcəkdə soruşula bilər.
- **OBS-3 — Log mesajının sözlüyü.** Task nümunəsi «İşçiyə 100 ₼ avans verildi» idi; faktiki mesaj «Günel Quliyeva — 100.00 AZN ödəniş verildi». AC5 «ad + məbləğ + növ» tələb edir və bu ödənilir, valyuta isə layihənin qalan hissəsindəki kimi «AZN»-dir (ardıcıllıq üstünlük təşkil edir). Mesaj «avans»-ı adi maaşdan ayırmır və `note` sahəsini daşımır.
- **OBS-4 — Silinmə log-unda kontekst azdır.** `"Maaş əməliyyatını sildi"` + `"{ad} — {məbləğ} AZN"`: sətrin növü (payment/deduction) və ayı log-a düşmür, ona görə audit feed-indən silinən sətrin kassaya təsiri olub-olmadığı görünmür.
- **OBS-5 — TC9-un bir yarısı üstüörtülü yoxlanılır.** «Ödəniş cari ayın xülasəsində görünmür» hissəsi ayrıca assertion deyil; ay filtri dəqiq bərabərlik (`e.Month == filter`) olduğu üçün nəticə məntiqən zəmanətlidir və keçmiş ay üçün `Assert.Single` sətir sayını sabitləyir.

## Senior-backend düzəlişinin doğrulanması

Senior review-də bildirilən `GetTodayClosingHandler` bug-u (`DateTime.UtcNow.Date` → `IDateProvider.Today`, ADR-0005) QA tərəfindən yenidən yoxlandı: `f05fc33` commit-i həqiqətən `IDateProvider`-i inject edir, tam test paketi **filtersiz, bir icrada** işlədildikdə `EmployeesApiTests.Salary_Payment_Is_Cash_Out_On_The_Dashboard` daxil olmaqla 544 testin hamısı keçir. Fləykilik müşahidə edilmədi (paket iki dəfə işlədildi, hər ikisində 544/0).

## Yekun qərar

**QA PASSED.** 16/16 AC ödənilir, 30/30 test case yaşıldır, build 0 Warning / 0 Error, 544/544 test keçir, bug tapılmadı. Kritik biznes qaydaları (qalıq = 420, ödənişin kassadan çıxması, tutulmanın kassaya toxunmaması, bağlanmış günün ikinci dəfə çıxılmaması, rol matrisi, cross-user qorunması) QA-nın öz müstəqil probe-ları ilə real host + real SQL Server üzərində təkrar-təsdiqləndi.

**Tövsiyə:** task `Done` statusuna keçirilsin. GAP-1, GAP-2 və OBS-1 gələcək kiçik tasklar üçün namizəddir (bu PR-ı bloklamır).

## Last Updated

2026-08-02 — BE#28 QA sessiyası (qa-tester).
