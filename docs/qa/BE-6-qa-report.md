# QA Report — BE#6: `GET /api/reports/summary` — `generalExpenses` / `productExpenses`

**Tarix:** 2026-07-27
**QA Agent:** qa-tester
**Test edilən branch:** `task/BE-6-summary-expense-split`, HEAD `0e423dc`
**Base branch (stacked PR #8):** `task/BE-4-expense-types-source`
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/6
**Mühit:** Lokal, Windows, .NET 8 SDK (`dotnet 9.0.306`), SQL Server (`localhost`) əlçatan idi — Unit + IntegrationTests hər ikisi canlı şəkildə icra olundu.

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 6 (AC-1..AC-6) |
| ✅ Pass | 6 |
| ❌ Fail | 0 |
| Ümumi TC | 4 (TC-B01..TC-B04) |
| ✅ Pass | 4 |
| ❌ Fail | 0 |
| Yaradılan bug sayı | 0 |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build MayaPro.WarehouseApi.sln -o <izolə qovluq> --no-incremental` → **Build succeeded, 0 Error(s)** (yalnız keçici `MSB3026` fayl-kopyalama xəbərdarlıqları — retry ilə həll olundu, kod xətası deyil).

Unit testlər: **124/124 keçdi** (bütün modullar, təzə izolə build-dən).
IntegrationTests: **119/119 keçdi** (canlı SQL Server `localhost`-da, `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` daxil).

## Mühit qeydi — DLL kilidi (gözlənilən, təsdiqləndi)

Tapşırıqda qeyd olunan mühit riski birbaşa təsdiqləndi: `dotnet build MayaPro.WarehouseApi.sln` (standart `bin/Debug/net8.0` çıxışı ilə) **24 x MSB3021/MSB3027** xətası ilə uğursuz oldu — hamısı `MayaPro.WarehouseApi.Api.csproj`-un DLL-lərini kopyalaya bilməməsi, "The file is locked by: Microsoft Visual Studio 2022 (30796), MayaPro.WarehouseApi.Api (3780)" səbəbindən. Bu, **kompilyasiya xətası DEYİL** — aşağıdakılarla təsdiqləndi:

1. `dotnet build src/MayaPro.WarehouseApi.Api/MayaPro.WarehouseApi.Api.csproj -o <təcrid olunmuş qovluq>` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
2. `dotnet build MayaPro.WarehouseApi.sln -o <təcrid olunmuş qovluq> --no-incremental` (bütün solution, IntegrationTests daxil) → **Build succeeded, 0 Error(s)**.

Nəticə: kilid, işləyən VS/`MayaPro.WarehouseApi.Api` prosesinin `bin/Debug/net8.0` qovluğuna yazma icazəsini tutmasından qaynaqlanır — məhsul kodu problemi DEYİL, mühit məhdudiyyətidir. Bütün sonrakı test icraları izolə edilmiş `-o` çıxışından aparıldı.

SQL Server `localhost`-da əlçatan idi, ona görə `MayaPro.WarehouseApi.IntegrationTests` (SQL Server tələb edən) bloklanmadı — tam icra olundu (bax aşağıda).

## Dəyişikliklərin baxışı (`origin/task/BE-4-expense-types-source...HEAD`)

Diff yalnız 2 fayla toxunur:
- `GetSummaryHandler.cs`: `generalExpenses` indi `expensesTotal - productExpenses` (əvvəlki: `Source == General` üzrə ayrıca filtr) — invariantı (AC-3) struktur olaraq təmin edir.
- `GetSummaryHandlerTests.cs`: 3 yeni unit test (TC-B03, TC-B04 + naməlum mənbə invariantı).

`SummaryDto.GeneralExpenses/ProductExpenses`, `ExpenseReportRow.Source`, handler-də bölgü məntiqinin özü — bunların hamısı base branch-da (PR #8) artıq mövcud idi, bu PR-də dəyişməyib.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Sübut |
|---|---|---|---|
| AC-1 | Cavabda `generalExpenses`/`productExpenses` var | ✅ | `SummaryDto.cs` sətir 33-34; `IntegrationTestHelpers.SummaryDto` bu sahələri deserializasiya edir; `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` (integration, keçdi). |
| AC-2 | Hər iki rəqəm dövrə (from/to) düzgün filtrlənir | ✅ | `GetSummaryHandler.cs` sətir 19: `expenses.GetExpensesAsync(window.From, window.To, ct)` — bölgü elə bu filtrlənmiş sətirlər üzərində aparılır (ayrıca sorğu yoxdur). Unit `Expenses_Outside_The_Period_Are_Excluded_From_Both_The_Split_And_The_Total` (TC-B03, yeni, keçdi); `The_Period_Window_Is_The_One_Passed_To_The_Expenses_Contract` (mövcud, keçdi). |
| AC-3 | `generalExpenses + productExpenses == expenses` (tam, yuvarlaqlaşdırma fərqi yoxdur) | ✅ | Refactor bunu **struktur olaraq** təmin edir: `generalExpenses = expensesTotal - productExpenses` ⇒ cəm həmişə `expensesTotal`-dır (decimal aritmetikası, yuvarlaqlaşdırma addımı yoxdur). Bütün 6 `GetSummaryHandlerTests` testi bunu ayrıca assert edir; integration testdə də `Assert.Equal(s.Expenses, s.GeneralExpenses + s.ProductExpenses)`. |
| AC-4 | Mala bağlı olmayan xərc yalnız `generalExpenses`-ə, mala bağlı yalnız `productExpenses`-ə düşür | ✅ (aşağıda təhlil) | Bax "AC-4 xüsusi təhlili" bölməsi — real (validasiyadan keçmiş) məlumat üçün əvvəlki davranışla riyazi olaraq eynidir. |
| AC-5 | Mövcud testlər keçir, yeni davranış üçün unit test əlavə olunub | ✅ | Mövcud 3 `GetSummaryHandlerTests` testi (`Splits_...`, `Split_Is_Zero_...`, `Only_General_...`) reqressiyasız keçir; 3 yeni test (TC-B03, TC-B04, naməlum mənbə) əlavə olunub, hamısı keçir. |
| AC-6 | Backend build və testlər xətasız | ✅ | İzolə edilmiş `-o` ilə build 0 Error(s); Unit 124/124; IntegrationTests 119/119. Standart `bin/Debug` ilə build MSB3021 verir, amma bu sənədləşdirilmiş mühit məhdudiyyətidir (yuxarıda), kod xətası deyil. |

### AC-4 xüsusi təhlili — refactor AC-4-ü pozurmu?

**Xeyr, real məlumat üzərində pozmur.** Səbəb:

- `Expenses.Domain.ExpenseSource` **qapalı enum**-dur: yalnız `General = 1` və `Product = 2` (`ExpenseSource.cs`). Wire tərəfdə də yalnız `"general"`/`"product"` (`WireFormat.ExpenseSources`).
- `CreateExpenseValidator`/`UpdateExpenseValidator` hər ikisi `Source`-u `ExpenseSourceCode.TryParse(code, out _)` ilə doğrulayır — bu, yalnız `"general"` və `"product"`-u qəbul edir, hər hansı digər dəyər **400 ilə rədd edilir** (`"Xərc mənbəyi yanlışdır"`). Deməli API vasitəsilə "naməlum mənbəli" xərc **heç vaxt yaradıla bilməz**.
- Bu iki dəyərli qapalı çərçivə daxilində, riyazi olaraq: `generalExpenses (yeni) = expensesTotal - productExpenses = (generalSum + productSum) - productSum = generalSum (köhnə eksplisit filtrin nəticəsi ilə eynidir)`. Yəni **real (validasiyadan keçmiş) məlumat üçün yeni kod köhnə kodla tam eyni nəticəni verir** — heç bir davranış fərqi yoxdur.
- Yeganə fərq **sırf nəzəri/müdafiə xəttli** ssenaridədir: `GetSummaryHandlerTests.A_Source_Outside_The_Known_Vocabulary_Still_Keeps_The_Split_Equal_To_The_Total` unit test səviyyəsində `"supplier"` kimi uydurma bir mənbə dəyərini sınayır (bu, real API-də mümkün olmayan bir sətirdir, yalnız handler-in unit-səviyyəli robustluğunu yoxlayır). Bu ssenaridə "naməlum mənbə" indi `generalExpenses`-ə düşür — köhnə kodda isə heç bir tərəfə düşməzdi (AC-3-ü poza-poza). Bu, AC-4-ün hərfi mətninə (yalnız iki tanınan mənbə arasında bölgü nəzərdə tutur) formal ziddiyyət yaratmır, çünki **AC-4 və AC-3 yalnız validasiyadan keçmiş, real mənbəli sətirlər üçün mənalıdır** — validasiya qatı bu sətirlərin mövcudluğunu artıq təmin edir.

**Nəticə:** Refactor AC-3-ü struktur olaraq gücləndirir (heç vaxt uğursuz ola bilməz), AC-4-ə isə real istifadə ssenarilərində heç bir təsir etmir, çünki domen validasiyası "naməlum mənbə" halının baş verməsinin qarşısını əvvəlcədən alır. Bloklayıcı deyil.

## Test Case nəticələri

| # | Ad | Nəticə | Test metodu | Qeyd |
|---|---|---|---|---|
| TC-B01 | 2 ümumi (50+30) + 1 mala bağlı (80) → general=80, product=80, expenses=160 | ✅ | `Splits_Expenses_By_Source_And_The_Split_Sums_To_The_Total` (mövcud, base branch-dan) | Test eyni struktur (2 general + 1 product) istifadə edir, amma fərqli ədədlərlə (100+50=150 general, 250 product, cəmi 400). Bölgü məntiqi eynidir, ədədi uyğunluq yoxdur, amma davranış tam yoxlanılıb. Bloklayıcı deyil — API-səviyyəli `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` (integration) də 100 general + 250 product ilə eyni ssenarini təsdiqləyir. |
| TC-B02 | Xərcsiz dövr → hər üç rəqəm 0 | ✅ | `Split_Is_Zero_When_There_Are_No_Expenses` (mövcud) | Dəqiq uyğun: general=0, product=0, expenses=0. |
| TC-B03 | Dövr sərhədindən kənar xərc → heç bir rəqəmə düşmür | ✅ | `Expenses_Outside_The_Period_Are_Excluded_From_Both_The_Split_And_The_Total` (YENİ, `d4a650f`) | Dünənki 500 (product) "bugün" sorğusuna düşmür; general=100, product=0, expenses=100 — dəqiq uyğun. |
| TC-B04 | Yalnız mala bağlı xərclər → general=0, product=toplam | ✅ | `Only_Product_Expenses_Leaves_GeneralExpenses_At_Zero` (YENİ, `d4a650f`) | 40+60 product → general=0, product=100, expenses=100 — dəqiq uyğun. |

## Regressiya

- `GetSummaryHandlerTests` (Reports.Tests) — bütün mövcud 6 test (base branch-dan) reqressiyasız keçir, 3 yeni ilə birgə 17/17 (modul daxilində, `DashboardCalculatorTests` daxil).
- Bütün digər unit test modulları (SharedKernel, Sales, Products, Suppliers, Expenses, Auth, DayEnd, Customers) — reqressiya yoxdur, 107/107.
- Bütün `IntegrationTests` (119 test: `ReportsApiTests`, `ExpensesApiTests`, `ExpenseTypesApiTests`, `SalesApiTests`, `DayEndApiTests`, `ProductsApiTests`, migrasiya testləri, `WireFormatApiTests` və s.) — 119/119 keçdi, reqressiya yoxdur.

## İcra olunan test əmrləri və rəqəmlər

```bash
git -C ".../backend" status
# On branch task/BE-6-summary-expense-split, up to date, clean

git -C ".../backend" diff origin/task/BE-4-expense-types-source...HEAD --stat
# GetSummaryHandler.cs (8 +/-3), GetSummaryHandlerTests.cs (+57)

# Reports.Tests — ilkin sürətli yoxlama (inkremental)
dotnet test tests/MayaPro.WarehouseApi.Modules.Reports.Tests
# Total tests: 17, Passed: 17, Failed: 0

# Standart bin/Debug ilə tam solution build — MÜHİT məhdudiyyəti təsdiqləndi
dotnet build MayaPro.WarehouseApi.sln
# 24 Error(s) — hamısı MSB3021/MSB3027, "locked by MayaPro.WarehouseApi.Api (running)/VS 2022"

# Api layihəsi tək başına, izolə çıxışla — kompilyasiya xətası olmadığı təsdiqləndi
dotnet build src/MayaPro.WarehouseApi.Api/MayaPro.WarehouseApi.Api.csproj -o <temp>/mayapro-api-build
# Build succeeded. 0 Warning(s). 0 Error(s).

# Bütün solution, izolə çıxış, TƏZƏ (--no-incremental)
dotnet build MayaPro.WarehouseApi.sln -o <temp>/mayapro-full-be6 --no-incremental
# Build succeeded. 0 Error(s). (yalnız keçici MSB3026 kopyalama xəbərdarlıqları, retry ilə həll oldu)

# Unit testlər — hər modul, EYNİ təzə izolə build-dən
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.SharedKernel.Tests.dll        # 6/6
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Sales.Tests.dll       # 20/20
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Products.Tests.dll    # 24/24
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Suppliers.Tests.dll   # 4/4
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Expenses.Tests.dll    # 39/39
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Reports.Tests.dll     # 17/17
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Auth.Tests.dll        # 4/4
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.DayEnd.Tests.dll      # 4/4
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.Modules.Customers.Tests.dll   # 6/6
# CƏMİ: 124/124 keçdi, 0 uğursuz

# IntegrationTests — canlı SQL Server (localhost), TƏZƏ izolə build-dən
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.IntegrationTests.dll
# Total tests: 119, Passed: 119, Failed: 0

# Xüsusi doğrulama: BE#6-ya birbaşa aid API-səviyyəli test
dotnet test <temp>/mayapro-full-be6/MayaPro.WarehouseApi.IntegrationTests.dll \
  --filter "FullyQualifiedName~Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total"
# Total tests: 1, Passed: 1
```

## İşlədilə bilməyən testlər

Yoxdur. Tapşırıqda gözlənilən risk (SQL Server əlçatan olmaya bilər, `IntegrationTests` işlədilə bilməyə bilər) bu sessiyada baş vermədi — `localhost` SQL Server əlçatan idi və `MayaPro.WarehouseApi.IntegrationTests` (o cümlədən `Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total`) tam, canlı şəkildə icra olundu və keçdi (119/119).

## Tapılan bug-lar

Yoxdur.

## Tövsiyələr

1. Standart `dotnet build`/`dotnet test` axınında (VS açıq ikən) davamlı MSB3021 kilid xətası ilə qarşılaşmamaq üçün CI/lokal skriptlərdə həmişə izolə edilmiş `-o` çıxış qovluğu istifadə edilməsi tövsiyə olunur (BE#4 QA report-unda da eyni tövsiyə var — təkrarlanan mühit riski).
2. Funksional dəyişiklik yoxdur, kod baxışı və test nəticələri əsasında bu task birbaşa Done-a keçirilə bilər.

## Yekun verdikt

**BE#6 Done-a hazırdır.** Build 0 Error(s) (izolə çıxışla), Unit 124/124, IntegrationTests 119/119 (`Summary_Splits_Expenses_By_Source_And_Sums_To_The_Total` daxil), bütün 6 AC və 4 TC PASS, reqressiya yoxdur, bug tapılmadı.
