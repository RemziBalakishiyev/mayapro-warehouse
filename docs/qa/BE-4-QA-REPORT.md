# QA Report — BE#4: İdarə olunan xərc növləri + xərc mənbəyi ayrımı

**Tarix:** 2026-07-27
**QA Agent:** qa-tester
**Test edilən branch:** `task/BE-4-expense-types-source`, HEAD `e29e96d` (senior-backend review-dan sonra)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/4
**Mühit:** Lokal, Windows, .NET 8 SDK, SQL Server (`localhost`) — `dotnet build`/`dotnet test` bütün solution üzərində, IntegrationTests canlı SQL Server-də icra olundu

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 8 (AC-1..AC-8) |
| ✅ Pass | 7 |
| ❌ Fail | 1 (AC-7) |
| Ümumi TC | 12 (TC-1..TC-12) |
| ✅ Pass | 10 |
| ❌ Fail | 2 (TC-8, TC-9) |
| Yaradılan bug sayı | 1 (BE#5, High) |
| **Yekun qərar** | **FAIL → In Progress (AC-7/TC-8/TC-9 üçün BE#5 həll edilməlidir)** |

Build: `dotnet build` (bütün solution, Api + IntegrationTests daxil, izolə edilmiş output qovluğuna) → **Build succeeded, 0 Warning(s), 0 Error(s)** (yalnız həlledici olmayan `NETSDK1194` — solution-level `-o` istifadəsi ilə bağlı informativ xəbərdarlıq, kod xətası deyil).

Unit testlər: **121/121 keçdi** — hər modul TƏZƏ (izolə edilmiş, `--no-incremental`) build-dən ayrıca doğrulandı, "yalançı yaşıl" riski yoxdur.

IntegrationTests: **icra olundu (bəli)** — canlı SQL Server `localhost`-da, cəmi **119 test: 112 keçdi, 7 uğursuz**. Uğursuz olan bütün 7 test **`ExpensesMigrationTests`**-dədir (AC-7/TC-8/TC-9) — bax "Kritik tapıntı" bölməsi.

## ⚠️ Kritik tapıntı — ilkin `dotnet test --no-build` nəticəsi yanlış idi (yalançı yaşıl)

Solution-u `dotnet build MayaPro.WarehouseApi.sln -o <izolə qovluq>` ilə build edəndə (VS-un `Api` DLL-lərini kilidləməsindən yayınmaq üçün — bax aşağıda), MSBuild `-o` ilə **bütün layihələrin** çıxışını həmin izolə qovluğa yönləndirir, lakin hər layihənin öz `bin/Debug/net8.0` qovluğuna YAZMIR. Ardınca `dotnet test MayaPro.WarehouseApi.sln --no-build` işə salınanda, o, hər layihənin **öz köhnə `bin/Debug/net8.0`** qovluğundakı DLL-i istifadə etdi — bu, `ExpensesMigrationTests.cs` və `ExpenseTypesApiTests.cs` fayllarının hələ mövcud olmadığı KÖHNƏ bir build idi (98 test, "98/98 keçdi" göstərdi — dəqiq PR şərhindəki rəqəmlə eyni).

Bunu QA prosesi zamanı aşkarladım: `--list-tests` ilə yoxlayanda `ExpensesMigrationTests`/`ExpenseTypesApiTests` siyahıda YOX idi. IntegrationTests layihəsini ayrıca, izolə edilmiş qovluğa **məcburi (`--no-incremental`) yenidən build** etdikdən sonra bu iki fayl kompilyasiyaya daxil oldu (csc.dll çağırışında təsdiqləndi) və ümumi test sayı **98 → 119**-a qalxdı. Yalnız bundan sonra əsl nəticə görünə bildi: **7 test uğursuz**.

**Nəticə:** əvvəlki "IntegrationTests 98/98 keçdi" iddiası (PR şərhi) texniki cəhətdən doğrudur, amma **BE#4-ün ən kritik testlərini (migrasiya) ehtiva etməyən köhnə bir build üzərində əsaslanıb** — bu, tapşırıqda xəbərdarlıq edilən "yalançı yaşıl" riskinin məhz özüdür.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Sübut |
|---|---|---|---|
| AC-1 | Xərc növü yaratma (201, id+name) | ✅ | `CreateExpenseTypeHandler.cs`; integration `ExpenseTypesApiTests.Create_ExpenseType_Then_It_Appears_In_List_And_Duplicate_Is_Rejected` (keçdi). |
| AC-2 | Dublikat xərc növü → 400 "Bu xərc növü artiq movcuddur" (case-insensitive) | ✅ | `CreateExpenseTypeHandler.cs` sətir 26-33: hər iki tərəf `.ToLower()` ilə müqayisə olunur (DB collation-dan asılı deyil); `ExpenseErrors.cs`: `"Bu xərc növü artıq mövcuddur"` — mesaj mətni AC ilə hərfi-hərfinə üst-üstə düşür. Integration `Duplicate_Is_Rejected_Case_Insensitively` (keçdi). |
| AC-3 | Seed 7 xərc növü (Yol pulu, Fəhlə pulu, Yer/Anbar xərci, Paket/Qutu, Gömrük, Mağaza xərci, Digər) | ✅ | `ExpenseTypeSeeder.cs` sətir 17-26 — 7 ad dəqiq uyğundur. Integration `Seeded_Default_Types_Are_Present` (keçdi). |
| AC-4 | General mənbəli xərc mayaya təsir etmir, `AddExpenseToProductAsync` çağırılmır | ✅ | `CreateExpenseHandler.cs` sətir 38-55: `AddExpenseToProductAsync` yalnız `source == ExpenseSource.Product` budağında çağırılır; general üçün heç toxunulmur. Eyni qayda `UpdateExpenseHandler`/`DeleteExpenseHandler`-da da (mock-la verify edilib). Unit: Expenses.Tests-də Create/Update/Delete üçün ayrıca handler testləri (39 test, "AddExpenseToProductAsync-in çağırılmadığı" birbaşa mock-la təsdiqlənir). Integration `General_Expense_Does_Not_Change_Any_Product_Cost` (keçdi). |
| AC-5 | Product mənbəli xərc — köhnə maya zənciri davranışı qorunur, `Source == product` saxlanılır | ✅ | Eyni handler-lərdə `source == ExpenseSource.Product` budağı `AddExpenseToProductAsync`/`RemoveExpenseFromProductAsync`-i çağırır (Create/Update/Delete). Integration: `Product_Linked_Expense_Increases_That_Products_Real_Cost`, `Update_Product_Linked_Expense_Reapplies_The_New_Amount_To_The_Cost`, `Delete_Product_Linked_Expense_Lowers_The_Products_Real_Cost_Back`, `Switching_A_Product_Expense_To_General_Gives_The_Products_Cost_Back` — hamısı keçdi. |
| AC-6 | `GET /api/expenses?source=...` filtri, hər elementdə `source` sahəsi | ✅ | `GetExpensesHandler.cs` sətir 17-38 — `source` parametri parse olunur, naməlum dəyər → `InvalidSource` (400), tanınan dəyər `Where(e => e.Source == filter)` ilə filtrlənir, `month` ilə birgə işləyir. Integration `Source_Filter_Returns_Only_Matching_Expenses_For_The_Month` (keçdi) — general/product filtrlərinin ayrı-ayrı işlədiyini və hər elementdə düzgün `source` sahəsini təsdiqləyir (qeyd: paylaşılan test DB-də dəqiq say yerinə `Contains`/`DoesNotContain` iddiaları istifadə olunur — TC-6/TC-7-nin "dəqiq say" tələbindən fərqli, amma filtr məntiqini eyni dərəcədə sübut edir, bax TC cədvəli). `WireFormatApiTests.Product_Expenses_Are_A_CamelCase_Name_Amount_Array` (keçdi) — `source` camelCase wire-da mövcuddur. |
| AC-7 | Migration: enum→Azərbaycanca ad, `Source` `ProductId`-dən backfill, sətir sayı dəyişməz | ❌ | **Kod baxışı ilə düzgün görünür** (`20260727120000_ExpenseTypesAndSource.cs`: 6 enum adı → 6 Azərbaycanca ad map-i tamdır, `Source` backfill-i hər iki budaqda `migrationBuilder.Sql(...)` literal SQL-lə açıq doldurulur, sonra `NOT NULL`-a keçir, default constraint yoxdur), **AMMA bunu sübut edən `ExpensesMigrationTests` canlı SQL Server-də 7/8 test ilə ÇÖKÜR** (`System.InvalidOperationException: The current provider doesn't have a store type mapping for properties of type 'DBNull'` — test helper-in `ExecuteSqlRawAsync(sql, object[])`-a çılpaq `DBNull.Value` ötürməsindən). Nəticədə AC-7 **heç bir keçən testlə sübut olunmur** → bax BE#5 (bug). |
| AC-8 | Summary-də `generalExpenses`/`productExpenses`, cəmi `expenses`-ə bərabər, `netProfit` dəyişməyib | ✅ | `GetSummaryHandler.cs` sətir 27-30, 46-47: `generalExpenses`/`productExpenses` `ExpenseReportRow.Source`-a görə bölünür, `NetProfit = profit - expensesTotal` düsturu toxunulmayıb (əvvəlki kimi tam `expensesTotal` istifadə edir, bölünmüş cəmlərdən deyil). Reports.Tests-də 4 yeni unit test (bölgü=cəm, netProfit dəyişməz). Integration `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` (keçdi). |

## Test Case nəticələri

| # | Ad | Nəticə | Qeyd |
|---|---|---|---|
| TC-1 | Xərc növü yaratma - happy path | ✅ | `Create_ExpenseType_Then_It_Appears_In_List_And_Duplicate_Is_Rejected` — 201, id+name=Sığorta (analoq ad), siyahıda görünür. |
| TC-2 | Xərc növu - dublikat ad | ✅ | `Duplicate_Is_Rejected_Case_Insensitively` — 400, "Bu xərc növü artıq mövcuddur", DB-də təkrar setir yoxdur (unique index + handler yoxlaması). |
| TC-3 | Seed dəyərlərinin qaytarılması | ✅ | `Seeded_Default_Types_Are_Present` — 7 ad dəqiq uyğundur. |
| TC-4 | General xərc - maya dəyişmir | ✅ | Unit: Expenses.Tests-də Create handler testi (`AddExpenseToProductAsync` çağırılmadığı mock-la verify edilir). Integration: `General_Expense_Does_Not_Change_Any_Product_Cost` — real cost dəyişməz qalır. |
| TC-5 | Product xərc - maya zənciri qorunur | ✅ | `Product_Linked_Expense_Increases_That_Products_Real_Cost` — real cost köhnə davranışa uyğun artır, `Source == product`. |
| TC-6 | Source filtri - general | ✅ (qeydlə) | `Source_Filter_Returns_Only_Matching_Expenses_For_The_Month` general branch-ı yoxlayır (`Contains`/`DoesNotContain` — paylaşılan test DB-də dəqiq "2 element" sayını deyil, filtrin düzgün ayırdığını sübut edir). Funksional olaraq AC-yə cavab verir, PM-in "dəqiq say" formatına hərfi uyğun deyil — bloklayıcı deyil. |
| TC-7 | Source filtri - product | ✅ (qeydlə) | Eyni test, product branch-ı — TC-6 ilə eyni qeyd. |
| TC-8 | Migration - kateqoriya adı çevrilməsi | ❌ | `ExpensesMigrationTests.Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name` (6 hal) — hamısı test setup-da (`InsertLegacyRowAsync`) `DBNull` xətası ilə çökür, assertion-a çatmır. **FAIL** (bax BE#5). |
| TC-9 | Migration - Source doldurulması | ❌ | `ExpensesMigrationTests.Migration_Renames_Legacy_Categories_And_Backfills_Source_From_ProductId` — eyni səbəbdən çökür. **FAIL** (bax BE#5). |
| TC-10 | Summary - mənbə üzrə bölgü | ✅ | `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` — generalExpenses+productExpenses=expenses, netProfit düzgün. |
| TC-11 | Xərc növü - boş/whitespace ad | ✅ | `Empty_ExpenseType_Name_Is_Rejected_With_400("")` və `("   ")` — `CreateExpenseTypeValidator.NotEmpty()` FluentValidation-da whitespace-only stringi də rədd edir (`IsNullOrWhiteSpace` semantikası), hər ikisi 400. |
| TC-12 | Naməlum source filtri (edge case) | ✅ | `Unknown_Source_Filter_Does_Not_Return_500` — 400 (`Expenses.InvalidSource`) seçilib və sənədləşdirilib (test şərhində), 500 atılmır. PM-in icazə verdiyi iki davranışdan biri, düzgün sənədləşdirilib. |

## Regressiya

Sales/Reports/DayEnd/Products/migrations üzrə mövcud integration testlər (40 test: `SalesApiTests`, `ReportsApiTests` [qalan], `DayEndApiTests`, `ProductsApiTests`, `ProductsMigrationTests`, `SalesMigrationTests`) canlı SQL Server-də ayrıca işə salındı → **40/40 keçdi**. Xərc/satış/gün-bağlanışı/hesabat axınlarında reqressiya aşkarlanmadı.

## İcra olunan test əmrləri və rəqəmlər

```bash
git -C ".../backend" status --short --branch
# ## task/BE-4-expense-types-source...origin/task/BE-4-expense-types-source (HEAD e29e96d)

# Build — bütün solution, izolə edilmiş output (VS-un Api DLL-lərini kilidləməsindən yayınmaq üçün)
dotnet build MayaPro.WarehouseApi.sln -o "C:\qa-build-be4"
# Build succeeded. 0 Warning(s) (kod xətası yox — yalnız NETSDK1194 informativ). 0 Error(s).

# Unit testlər — hər modul TƏZƏ (--no-incremental) izolə build-dən, staleness riski aradan qaldırılıb
# SharedKernel 6/6, DayEnd 4/4, Reports 14/14, Customers 6/6, Sales 20/20,
# Products 24/24, Suppliers 4/4, Expenses 39/39, Auth 4/4
# CƏMİ: 121/121 keçdi, 0 uğursuz

# IntegrationTests — canlı SQL Server (localhost), TƏZƏ (--no-incremental) izolə build
dotnet build tests/MayaPro.WarehouseApi.IntegrationTests/... --no-incremental -o "C:\qa-build-be4-it"
dotnet test "C:\qa-build-be4-it\MayaPro.WarehouseApi.IntegrationTests.dll"
# Total tests: 119, Passed: 112, Failed: 7
# Failed (hamısı): ExpensesMigrationTests.Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name (6 hal)
#                  ExpensesMigrationTests.Migration_Renames_Legacy_Categories_And_Backfills_Source_From_ProductId

# Regressiya alt-dəsti (Sales/Reports/DayEnd/Products/migrations)
dotnet test "...\MayaPro.WarehouseApi.IntegrationTests.dll" --filter "...SalesApiTests|...ReportsApiTests|...DayEndApiTests|...ProductsMigrationTests|...ProductsApiTests|...SalesMigrationTests"
# Total tests: 40, Passed: 40, Failed: 0
```

## Tapılan bug-lar

| ID | Başlıq | Ciddilik | Təsirlənən AC/TC |
|---|---|---|---|
| BE#5 | `ExpensesMigrationTests` test helper-i `DBNull.Value`-nu `ExecuteSqlRawAsync(sql, object[])`-a çılpaq ötürür → `InvalidOperationException` runtime-da, setup mərhələsində çökür | High | AC-7, TC-8, TC-9 |

Ətraflı: https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/5

## Tövsiyələr

1. **BE#5 həll edilməli** — `InsertLegacyRowAsync`-da `DBNull.Value`-nu açıq `SqlParameter`-ə (və ya `ExecuteSqlInterpolatedAsync`-ə) keçirmək kifayət edəcək (kiçik, aşağı riskli dəyişiklik). Düzəlişdən sonra `ExpensesMigrationTests` yenidən canlı SQL Server-də işə salınmalı və AC-7/TC-8/TC-9 üçün əsl (yaşıl) nəticə əldə olunmalıdır — bu, BE#4-ün Done olması üçün ZƏRURİDİR (migration logic statik baxışla düzgün görünsə də, sənədləşdirilmiş "keçdi" statusu olmadan risk kimi qalır).
2. CI/lokal axınında bundan sonra IntegrationTests-i **həmişə** ayrıca izolə edilmiş (`-o`) qovluqla, `--no-incremental` ilə (və ya VS bağlı olmadan) işə salmaq tövsiyə olunur — əks halda `-o` ilə solution-level build-in hər layihənin öz `bin`-inə yazmaması səbəbindən köhnə/yarımçıq binary-lər üzərində "yaşıl" nəticə əldə etmək riski var (bu QA sessiyasında bir dəfə baş verdi).
3. TC-6/TC-7-nin "dəqiq say" formatına daha sərt uyğunluq üçün `Source_Filter_Returns_Only_Matching_Expenses_For_The_Month` testinə izolə edilmiş say assertion-u əlavə oluna bilər — bloklayıcı deyil, gələcək təmizlik kimi qeyd olunur.
4. Backend taskı **Done**-a keçirilməməlidir — BE#5 həll olunub yenidən doğrulanana qədər "In Progress" saxlanmalıdır.

---

## RETEST — 2026-07-27 (commit `9f8abfa`, branch `task/BE-4-expense-types-source`)

**QA Agent:** qa-tester (təkrar verifikasiya, yeni feature taskı yaradılmadı)
**Test edilən commit:** `9f8abfa` — `fix(BE#5): migrasiya testlerinde NULL parametrlerin oturulmesi`
**Developer iddiası:** IntegrationTests 119/119, Unit 121/121, `ExpensesMigrationTests` 8/8.
**Metodologiya:** əvvəlki turdakı "yalançı yaşıl" tələsi TƏKRAR tətbiq edildi — solution `dotnet build ... --no-incremental -o <izolə qovluq>` ilə tam yenidən build edildi, sonra hər DLL üzərində `dotnet test` ayrıca işə salındı (heç bir `--no-build`/köhnə `bin/Debug` istifadə olunmadı). Nəticələr developer-in rəqəmlərini olduğu kimi qəbul etmədən, MÜSTƏQİL yoxlanıldı.

### Git vəziyyəti

```
git -C "<backend>" status --short --branch
## task/BE-4-expense-types-source...origin/task/BE-4-expense-types-source

git -C "<backend>" log --oneline -3
9f8abfa fix(BE#5): migrasiya testlerinde NULL parametrlerin oturulmesi
e29e96d docs(BE#4): review duzelislerinin senedlesdirilmesi
6277e06 test(BE#4): general xercin mayaya tesirsizliyi ucun handler testleri
```

Yalnız dəyişən fayllar: `tests/.../ExpensesMigrationTests.cs` (test helper) və `docs/qa/BE-4-QA-REPORT.md` (dev-in özü tərəfindən yaddaş üçün əlavə edilmiş qeyd) — **istehsalat migrasiya faylı (`20260727120000_ExpenseTypesAndSource.cs`) TOXUNULMAYIB**, ona görə AC-1..AC-6/AC-8-ə reqressiya riski strukturca yoxdur (aşağıda test icrası ilə də təsdiqləndi).

### Düzəlişin kod baxışı

`InsertLegacyRowAsync` indi `(object?)productId ?? DBNull.Value` çılpaq `object[]` elementi əvəzinə tam formalaşmış `Microsoft.Data.SqlClient.SqlParameter` obyektləri ötürür (`productId` → `SqlDbType.UniqueIdentifier`, `note` → `SqlDbType.NVarChar(500)`). Bu, EF Core-un `RawSqlCommandBuilder`-in `DBNull` CLR tipi üçün store-type mapping axtarmasının qarşısını alır (kök səbəb BE#5-də düzgün diaqnostika edilmişdi) — kod baxışı ilə düzgün və minimal-risklidir, digər heç bir sətrə toxunulmayıb.

### Müstəqil test icrası (təzə, izolə, `--no-incremental` build)

```
dotnet build MayaPro.WarehouseApi.sln -o "C:\qa-retest-be4" --no-incremental
# Build succeeded. 0 Warning(s). 0 Error(s).  (NETSDK1194 belə bu dəfə görünmədi)

dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.IntegrationTests.dll" --list-tests
# ExpensesMigrationTests-in bütün 8 üzvü (1 Fact + 6 Theory hal + 1 constraint testi) siyahıda VAR
# (əvvəlki turdakı "köhnə bin/Debug" yalançı-yaşıl riski bu dəfə TƏKRARLANMADI, siyahı təzə DLL-dən oxundu)

dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.IntegrationTests.dll"
# Test Run Successful. Total tests: 119. Passed: 119. (0 Failed, 0 Skipped)

dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.IntegrationTests.dll" --filter "FullyQualifiedName~ExpensesMigrationTests"
# Total tests: 8. Passed: 8.
#   Migration_Renames_Legacy_Categories_And_Backfills_Source_From_ProductId          [327 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Transport→Yol pulu) [375 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Labor→Fəhlə pulu)   [935 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Storage→Yer/Anbar xərci) [354 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Packaging→Paket/Qutu)    [377 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Store→Mağaza xərci) [345 ms]
#   Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name(Other→Digər)        [339 ms]
#   Migration_Leaves_Source_Required_And_Without_A_Lingering_Default_Constraint      [395 ms]

# Unit testlər — hər modul DLL-i, EYNİ izolə build-dən, ayrı-ayrı
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.SharedKernel.Tests.dll"          # 6/6
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.DayEnd.Tests.dll"        # 4/4
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Reports.Tests.dll"       # 14/14
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Customers.Tests.dll"     # 6/6
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Sales.Tests.dll"         # 20/20
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Products.Tests.dll"      # 24/24
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Suppliers.Tests.dll"     # 4/4
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Expenses.Tests.dll"      # 39/39
dotnet test "C:\qa-retest-be4\MayaPro.WarehouseApi.Modules.Auth.Tests.dll"          # 4/4
# CƏMİ: 121/121 keçdi, 0 uğursuz, 0 skipped
```

**Developer-in iddiası TAM TƏSDİQLƏNDİ, dəyişiklik yoxdur (rəqəmlər eyni, yalançı-yaşıl aşkarlanmadı):** Build 0/0, Unit 121/121, IntegrationTests 119/119, `ExpensesMigrationTests` 8/8.

### AC-7 — indi HƏQİQƏTƏN sübut olunurmu? (assertion-lar oxundu, sadəcə "yaşıl"a baxılmadı)

`tests/MayaPro.WarehouseApi.IntegrationTests/ExpensesMigrationTests.cs` faylının assertion-ları birbaşa oxundu:

- **Enum → Azərbaycanca ad çevrilməsi (bütün 6 üzv):** `Migration_Maps_Every_Legacy_Category_To_Its_Azerbaijani_Name` — `[InlineData]` ilə 6 hal: Transport→Yol pulu, Labor→Fəhlə pulu, Storage→Yer/Anbar xərci, Packaging→Paket/Qutu, Store→Mağaza xərci, Other→Digər. Hər hal köhnə sxemə (`BeforeMigration = "20260711183456_RenameCategoryValues"`) qədər migrasiya edir, köhnə enum adı ilə sətir yazır, sonra `db.Database.MigrateAsync()` ilə `ExpenseTypesAndSource` migrasiyasını tətbiq edir və `Assert.Equal(expected, row.Category)` ilə DB-dən oxunan həqiqi dəyəri yoxlayır — bu, mock deyil, canlı SQL Server-də real `UPDATE ... SET [Category] = N'...'` sətirlərinin icra olunduğunu sübut edir. **8/8 keçdi → 6/6 kateqoriya adı sübutlandı.**
- **Source backfill (`ProductId` → product/general):** `Migration_Renames_Legacy_Categories_And_Backfills_Source_From_ProductId` — iki sətir yazılır: biri `ProductId` dolu (`productLinkedId`), biri boş (`generalId`). Migrasiyadan sonra `Assert.Equal("product", productLinked.Source)` və `Assert.Equal("general", general.Source)` — hər iki budaq ayrıca yoxlanılır, sadəcə birinin keçməsi kifayət etmir, hər ikisi keçdi.
- **Sətir sayı və data dəyişməzliyi:** eyni testdə `Assert.Equal(countBefore, countAfter)` (heç bir sətir itmir/dublikatlaşmır), `Assert.Equal(100m, productLinked.Amount)`, `Assert.Equal("Bir qeyd", productLinked.Note)` (Amount/Note toxunulmadan qalır) — keçdi.
- **NOT NULL / default constraint davranışı:** `Migration_Leaves_Source_Required_And_Without_A_Lingering_Default_Constraint` — `INFORMATION_SCHEMA.COLUMNS`-dan `IS_NULLABLE = 'NO'` (Source constraint edilib) və `sys.default_constraints`-dən sayı `0` (heç bir gizli DEFAULT constraint qalmayıb, schema drift riski yoxdur) — hər ikisi `Assert.Equal` ilə birbaşa DB metadata-sından yoxlanılır, keçdi.

Bu 4 iddia (kateqoriya adı, Source backfill hər iki budaq, sətir sayı/data toxunulmazlığı, NOT NULL/constraint təmizliyi) məhz AC-7-nin mətni ilə üst-üstə düşür və indi hamısı canlı SQL Server-də, təzə izolə build-də, real assertion-larla **KEÇİR**. Migrasiya faylının özü bu turda dəyişməyib (əvvəlki QA-da artıq kod baxışı ilə düzgün tapılmışdı) — indi bunun sübutu da mövcuddur.

**AC-7 nəticəsi: ✅ PASS** (əvvəlki ❌ FAIL-dan dəyişdi).

### Digər AC-lərdə reqressiya yoxlanışı (AC-1..AC-6, AC-8)

Fix yalnız test helper-ə toxunduğu üçün struktur baxımından risk yoxdur, amma tam təsdiq üçün bütün 119 IntegrationTests (bu AC-lərə aid `ExpenseTypesApiTests`, `ExpensesApiTests`, `ReportsApiTests`, `WireFormatApiTests` daxil olmaqla) yenidən icra edildi → **119/119 keçdi, 0 uğursuz**. Əlavə olaraq regressiya alt-dəsti (`SalesApiTests`/`ReportsApiTests`/`DayEndApiTests`/`ProductsApiTests`/`ProductsMigrationTests`/`SalesMigrationTests`) ayrıca filtrlə işə salındı → **40/40 keçdi**. Heç bir reqressiya tapılmadı.

### Yekun AC cədvəli (retest-dən sonra)

| AC | Təsvir | Əvvəlki | Retest | Sübut |
|---|---|---|---|---|
| AC-1 | Xərc növü yaratma | ✅ | ✅ | Dəyişməyib, 119/119-un içində keçdi. |
| AC-2 | Dublikat xərc növü → 400 | ✅ | ✅ | Dəyişməyib, keçdi. |
| AC-3 | Seed 7 xərc növü | ✅ | ✅ | Dəyişməyib, keçdi. |
| AC-4 | General xərc mayaya təsir etmir | ✅ | ✅ | Dəyişməyib, keçdi. |
| AC-5 | Product xərc — maya zənciri qorunur | ✅ | ✅ | Dəyişməyib, keçdi. |
| AC-6 | Source filtri + wire-da `source` | ✅ | ✅ | Dəyişməyib, keçdi. |
| AC-7 | Migration: enum→ad, Source backfill | ❌ | **✅** | `ExpensesMigrationTests` indi canlı SQL Server-də 8/8 keçir, assertion-lar AC mətni ilə üst-üstə düşür (yuxarıda ətraflı). BE#5 bağlanır. |
| AC-8 | Summary source bölgüsü | ✅ | ✅ | Dəyişməyib, keçdi. |

**Ümumi:** 8/8 AC ✅ PASS. 12/12 TC ✅ PASS (TC-8/TC-9 artıq keçir; TC-6/TC-7 əvvəlki "dəqiq say" qeydi bloklayıcı olmadığı üçün Pass olaraq qalır).

### BE#5 (bug) statusu

**Həll edilib, bağlana bilər.** Kök səbəb düzgün diaqnostika edilib (`DBNull.Value`-nun çılpaq `object[]` elementi kimi ötürülməsi) və düzəliş (`SqlParameter` ilə əvəzləmə) canlı SQL Server-də 8/8 `ExpensesMigrationTests` ilə təsdiqlənib. İstehsalat kodunda heç bir dəyişiklik tələb olunmurdu (bug yalnız test helper-də idi) — bu, BE#5-in orijinal "Qeyd — təsirlənməyən sahələr" bölməsi ilə üst-üstə düşür.

### Yeni bug

Bu retest zamanı **yeni bug tapılmadı**.

### Yekun verdikt

**BE#4 Done-a hazırdır.** Build 0/0, Unit 121/121, IntegrationTests 119/119 (o cümlədən `ExpensesMigrationTests` 8/8), bütün 8 AC və 12 TC PASS, reqressiya yoxdur. BE#5 bağlana bilər.
