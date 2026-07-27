# QA Report — BE-9: Xərc tarixi gələcək ola bilməz (Create/Update validasiyası)

**Tarix:** 2026-07-27
**QA Agent:** qa-tester
**Test edilən:** Issue https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/9, branch `task/BE-9-expense-future-date`, commit `7a95b82` (HEAD, senior-in refactor/tighten commiti)
**Mühit:** Lokal, Windows, .NET 8 SDK (dotnet SDK 9.0.306 host), SQL Server (localhost, `MayaProWarehouse_Test` inteqrasiya test DB-si) — `dotnet build` / `dotnet test` bütün solution üzərində. API-nin standart `bin/Debug` çıxışı digər proseslər tərəfindən kilidli ola biləcəyi üçün (VS/başqa debug sessiyası) tapşırıqda göstərildiyi kimi `-p:BaseOutputPath=bin-qa/` alternativ çıxış qovluğu ilə build/test icra olundu. Bu mühit qeydi olaraq sənədləşdirilir, bug sayılmır.

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 6 (AC1–AC6) |
| Ümumi test case (task təsvirindəki TC + əlavələr) | 7 (TC-01…TC-04 + 3 əlavə/regressiya bloku) |
| ✅ Pass | 6/6 AC + 7/7 TC ssenari bloku |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Yaradılan bug sayı | 0 |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build -p:BaseOutputPath=bin-qa/` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test: `dotnet test -p:BaseOutputPath=bin-qa/` (bütün solution) → **211/211 keçdi**, 0 uğursuz, 0 skip.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC1 | POST /api/expenses gələcək tarixli `date` ilə 400 qaytarır, mesaj aydındır | ✅ PASS | `CreateExpenseValidator.cs:23-26` — `dateProvider.ToLocalDate(date) <= dateProvider.Today` şərti, `date is not null` olduqda işə düşür, mesaj: `"Xərcin tarixi gələcək ola bilməz"`. İnteqrasiya: `Future_Dated_Expense_Returns_400_And_Writes_No_Expense` — real HTTP üzərində 400, `error.Code == "General.Validation"`, `error.Message == "Xərcin tarixi gələcək ola bilməz"`, siyahıda yazılmadığı təsdiqlənir. Unit: `Tomorrows_Date_Is_Invalid` (TC-01). |
| AC2 | Xərc redaktə (update) endpoint-i eyni qaydaya tabedir | ✅ PASS | `UpdateExpenseValidator.cs:24-27` eyni qayda. `UpdateExpenseHandler.cs:30-32` — validasiya `dayEnd.ClosingExistsAsync` yoxlamasından (sətir 43-47) ƏVVƏL işləyir, yəni gələcək tarixli düzəliş 409 yox, həmişə 400 qaytarır. İnteqrasiya: `Update_To_A_Future_Date_Returns_400` — 400, mesaj doğru. Unit: `UpdateExpenseValidatorTests.Tomorrows_Date_Is_Invalid`. |
| AC3 | Bugünkü və keçmiş tarixli xərclərin yaradılması/redaktəsi pozulmur | ✅ PASS | Unit: `Todays_Date_Passes`, `Past_Date_Passes` (hər iki validator, TC-02/TC-03). İnteqrasiya: `Today_Dated_Expense_Is_Accepted` (201), `Update_Product_Linked_Expense_Reapplies_The_New_Amount_To_The_Cost` və digər mövcud `date=null` (bugün) ssenariləri 201/200 ilə keçir. |
| AC4 | "Bugün" Asia/Baku üzrə hesablanır (IDateProvider), UTC gecə yarısı sürüşməsi yoxdur | ✅ PASS | `AppDateProvider.ToLocalDate` `TimeZoneInfo.ConvertTimeFromUtc` ilə konfiqurasiya olunan zonaya (default `Asia/Baku`, `appsettings.json:13`) çevirir; `CreateExpenseHandler`/`UpdateExpenseHandler` default tarixi artıq `DateTime.UtcNow` yox, `dateProvider.UtcNow` ilə yazır (eyni saat mənbəyi). Unit: `Todays_Date_Passes_During_The_Baku_Early_Morning` (TC-04, "now" = 01:30 Baku / 21:30 UTC-26-da, bugünkü Bakı tarixi 27-si rədd edilmir) və `Instant_That_Is_Already_Tomorrow_In_Baku_Is_Invalid` (güzgü ssenari: UTC-də hələ bugün, lakin Bakıda artıq sabah olan an — 20:00Z — rədd edilir) hər iki validator üçün mövcud və yaşıl. |
| AC5 | Mövcud testlər keçir; yeni validasiya üçün unit test əlavə olunub | ✅ PASS | `Modules.Expenses.Tests` 7 → 20 test (13 yeni: `CreateExpenseValidatorTests` 7, `UpdateExpenseValidatorTests` 6). Bütün solution 211/211 yaşıl, heç bir mövcud test pozulmayıb. |
| AC6 | `dotnet build` xətasız | ✅ PASS | `dotnet build -p:BaseOutputPath=bin-qa/` → **0 Warning(s), 0 Error(s)**, bütün 22 layihə uğurla kompilyasiya olundu. |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Qeyd |
|---|---|---|---|
| TC-01 | date = sabah → 400, validasiya mesajı | ✅ PASS | Unit (`CreateExpenseValidatorTests.Tomorrows_Date_Is_Invalid`, `UpdateExpenseValidatorTests.Tomorrows_Date_Is_Invalid`) + inteqrasiya (`Future_Dated_Expense_Returns_400_And_Writes_No_Expense`, `Update_To_A_Future_Date_Returns_400`) — hamısı 400 + `"Xərcin tarixi gələcək ola bilməz"`, xərc yazılmır. |
| TC-02 | date = bugün → 201, uğurlu | ✅ PASS | Unit `Todays_Date_Passes` (hər iki validator) + inteqrasiya `Today_Dated_Expense_Is_Accepted` (201). |
| TC-03 | date = keçmiş tarix → 201, uğurlu | ✅ PASS | Unit `Past_Date_Passes` (hər iki validator, `2026-01-01`). |
| TC-04 | Bakı vaxtı ilə gecə 00:00-04:00 aralığında bugünkü tarix rədd olunmur (UTC sürüşməsi) | ✅ PASS | `Todays_Date_Passes_During_The_Baku_Early_Morning` — "indi" 01:30 Bakı / 2026-07-26 21:30Z, `date=2026-07-27T00:00:00Z` göndərilir → keçir (UTC təqvimi ilə "sabah" görünsə də, Bakı təqviminə görə bugündür). Hər iki validator üçün mövcud. |
| Əlavə | UTC-də hələ bugün, Bakıda artıq sabah olan an (məs. `...T20:00Z`) → rədd olunmalıdır | ✅ PASS | `Instant_That_Is_Already_Tomorrow_In_Baku_Is_Invalid` — "indi" 2026-07-27 10:00Z (14:00 Bakı), `date=2026-07-27T20:00:00Z` (00:00 Bakı, 28-i) göndərilir → rədd edilir, mesaj doğru. Hər iki validator üçün mövcud — TC-04-ün güzgüsü, sürüşmə hər iki istiqamətdə yoxlanılıb. |
| Əlavə | date = null → pozulmur | ✅ PASS | `Omitted_Date_Passes` (create — "indi" yazılır) və `Omitted_Date_Passes_And_Keeps_The_Existing_Date` (update — mövcud tarix qalır) — qayda `date is not null` şərti ilə işə düşmür, hər ikisi keçir. Bonus: `Future_Date_Without_A_Kind_Is_Invalid` — `DateTimeKind.Unspecified` gövdə (offset-siz JSON) də düzgün rədd edilir, atmır/keçmir. |
| Regressiya | Reports/GetSummary və digər modullar | ✅ PASS | `ReportsApiTests.Summary_Today_Aggregates_And_Is_Self_Consistent`, `Summary_Week_Spans_Seven_Days_And_All_Is_Unbounded` yaşıl. Bütün digər modul testləri (Sales 20, DayEnd 4, Reports 10, Customers 6, Products 24, Suppliers 12, Auth 4, SharedKernel 6) və qalan 102 mövcud inteqrasiya testi (o cümlədən `Product_Linked_Expense_Increases_That_Products_Real_Cost`, `Delete_Product_Linked_Expense_Lowers_The_Products_Real_Cost_Back`, `Update_Product_Linked_Expense_Reapplies_The_New_Amount_To_The_Cost`) dəyişikliksiz yaşıl qalır. |

## Müstəqil yoxlamalar (kod baxışı + build/test icrası ilə təsdiqlənib)

- **Kod baxışı**: `CreateExpenseValidator.cs`, `UpdateExpenseValidator.cs` — hər ikisi `IDateProvider` inject edir, `RuleFor(x => x.Date).Must(date => dateProvider.ToLocalDate(date!.Value) <= dateProvider.Today).When(x => x.Date is not null)` — eyni şərt, eyni mesaj. `CreateExpenseHandler.cs:35` — `command.Date ?? dateProvider.UtcNow` (əvvəllər `DateTime.UtcNow` idi) — validator və handler eyni saat mənbəyini istifadə edir, "indi" göndərilən default tarixin özü-özünü rədd etmə riski aradan qalxıb.
- **Validasiya sırası (UpdateExpenseHandler)**: `Handle` metodunda validator (sətir 30-32) `dayEnd.ClosingExistsAsync` bağlı-gün yoxlamalarından (sətir 43-47) ƏVVƏL çağırılır — sənədlərdə iddia edilən "gələcək tarixli düzəliş həmişə 400, heç vaxt 409 deyil" davranışı kodda təsdiqləndi və `Update_To_A_Future_Date_Returns_400` testi ilə örtülüb.
- **Sürüşmə istiqamətləri**: hər iki istiqamət (Bakı gecə erkəni UTC-də "hələ dünən" görünür → keçməli; UTC axşamı Bakıda "artıq sabah" görünür → rədd olunmalı) hər iki validator üçün ayrıca unit testlə örtülüb — sadəcə bir istiqamətin yoxlanılması riski yoxdur.
- **`DateTimeKind.Unspecified` halı**: `Future_Date_Without_A_Kind_Is_Invalid` — offset-siz JSON body-dən gələn tarixin `AppDateProvider.ToLocalDate` → `DateTime.SpecifyKind(utc, DateTimeKind.Utc)` yolu ilə düzgün işləndiyi, nə atma (exception) nə də səhv keçid olmadığı təsdiqləndi.
- **Sənəd–kod uyğunluğu**: `docs/business/BUSINESS-RULES.md` (yeni "Xərc qaydaları" bölməsi), `docs/flows/EXPENSE-COST-FLOW.md` (validation addımı yenilənib), `docs/changes/CHANGELOG.md` (BE#9 girişi) — hamısı faktiki kod davranışı ilə üst-üstə düşür (yoxlanılıb sətir-sətir).
- **Canlı HTTP səviyyəsi**: `ExpensesApiTests` real `WebApplicationFactory` + real lokal SQL Server (`MayaProWarehouse_Test`) üzərində işləyir — bu, ayrıca `curl` smoke testinə bərabər/ekvivalent HTTP-səviyyəli sübutdur; 105/105 inteqrasiya testi (BE#9-un 3 yeni ssenarisi daxil) bu real mühitdə yaşıldır.
- **Mühit qeydi**: standart `bin/Debug` çıxışı bu sessiyada əlçatan idi, amma tapşırıqdakı ehtiyat qeydinə uyğun olaraq `-p:BaseOutputPath=bin-qa/` istifadə olundu (senior-in `bin-review/`-ə bənzər yanaşma). Bu bug sayılmır, sadəcə mühit qeydidir.

## İcra olunan test əmrləri

```bash
git -C ".../backend" status
# On branch task/BE-9-expense-future-date, up to date with origin, clean

git -C ".../backend" log --oneline -2
# 7a95b82 refactor(BE-9): tighten the expense future-date rule and its coverage
# 7cb5485 feat(BE-9): reject future-dated expenses in Create/Update validators

dotnet build -p:BaseOutputPath=bin-qa/
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test -p:BaseOutputPath=bin-qa/
# MayaPro.WarehouseApi.SharedKernel.Tests            6/6 passed
# MayaPro.WarehouseApi.Modules.DayEnd.Tests          4/4 passed
# MayaPro.WarehouseApi.Modules.Sales.Tests           20/20 passed
# MayaPro.WarehouseApi.Modules.Reports.Tests         10/10 passed
# MayaPro.WarehouseApi.Modules.Expenses.Tests        20/20 passed (13 yeni BE#9 testi daxil)
# MayaPro.WarehouseApi.Modules.Customers.Tests       6/6 passed
# MayaPro.WarehouseApi.Modules.Products.Tests        24/24 passed
# MayaPro.WarehouseApi.Modules.Suppliers.Tests       12/12 passed
# MayaPro.WarehouseApi.Modules.Auth.Tests            4/4 passed
# MayaPro.WarehouseApi.IntegrationTests              105/105 passed (3 yeni BE#9 ssenarisi daxil, real SQL Server üzərində)
# TOTAL: 211/211 passed, 0 failed, 0 skipped

dotnet test tests/MayaPro.WarehouseApi.Modules.Expenses.Tests -p:BaseOutputPath=bin-qa/ \
  --filter "FullyQualifiedName~CreateExpenseValidatorTests|FullyQualifiedName~UpdateExpenseValidatorTests" -v n
# 13/13 passed (TC-01…TC-04 + əlavə/güzgü ssenariləri, hər iki validator üçün ayrı-ayrı)

dotnet test tests/MayaPro.WarehouseApi.IntegrationTests -p:BaseOutputPath=bin-qa/ \
  --filter "FullyQualifiedName~SummaryApiTests|FullyQualifiedName~Summary" -v n
# 3/3 passed (Reports/GetSummary regressiyası)
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; branch `task/BE-9-expense-future-date` QA-nı problemsiz keçdi.
- Bug tapılmadı — backend taskı **Done** statusuna keçirilə bilər.
