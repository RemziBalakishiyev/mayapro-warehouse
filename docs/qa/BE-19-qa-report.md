# QA Report — BE#19: Hesabatlar (summary + sales.pdf) qismən ödənişli satışda dashboard ilə üst-üstə düşmür

**Tarix:** 2026-07-30
**QA Agent:** qa-tester
**Test edilən PR(lar):** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/20 (branch `task/BE-19-reports-received-amount`, commit `db76c48`)
**Mühit:** Lokal, Windows, .NET 8 SDK, `dotnet build` / `dotnet test` (tam solution, 11 test layihəsi)

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi test case | 10 (AC-lərdəki TC1–TC10) |
| ✅ Pass | 10 |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Yaradılan bug sayı | 0 |
| **Yekun qərar** | **PASS → Done** |

## Build / Test rəqəmləri (bu QA sessiyasında bilavasitə icra edilib)

- `dotnet build MayaPro.WarehouseApi.sln` → **Build succeeded, 0 Warning(s), 0 Error(s)**
- `dotnet test MayaPro.WarehouseApi.sln --no-build` (bütün 11 test layihəsi) → **465/465 yaşıl, 0 uğursuz, 0 skipped**
  - Modul üzrə bölgü: DayEnd 4, Reports 22, Customers 6, SharedKernel 36, Suppliers 12, Expenses 52, Sales 48, Exports 46, Products 71, Auth 4, IntegrationTests 164 → cəm 465.
  - PR/senior-backend-in bəyan etdiyi 465/465 rəqəmi bilavasitə təkrarlanaraq təsdiqləndi.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC1 | Summary Cash/Card real alınan məbləğdən (`ReceivedVia`/`ReceivedAmount`) hesablanır | ✅ | `GetSummaryHandler.cs:37` → `salesRows.ComputeReceivedTotals()`; köhnə `PaymentType==X && TotalAmount` düsturu qalmayıb. |
| AC2 | Credit yalnız qalıqların cəmidir (`TotalAmount - ReceivedAmount`) | ✅ | `SalesReportRowTotals.cs:50` — `credit += row.TotalAmount - row.ReceivedAmount` yalnız `PaymentType==Credit` sətirləri üçün. |
| AC3 | `ExportSalesPdf` eyni məntiqlə hesablayır, PDF-dəki rəqəmlər summary ilə bitə-bitə eynidir | ✅ | `ExportSalesPdfHandler.cs:43` eyni `ComputeReceivedTotals()`-ı çağırır; hər iki handler eyni giriş dəstindən eyni `SalesDayTotals` alır. |
| AC4 | Dublikat məntiq yoxdur — ortaq helper istifadə olunur | ✅ | `SalesReportRowTotals.ComputeReceivedTotals` (SharedKernel) həm `GetSummaryHandler`, həm `ExportSalesPdfHandler` tərəfindən çağırılır. `DashboardCalculator.ExpectedCash` da artıq `ReceivedVia`/`ReceivedAmount` üzərindədir (3-cü müstəqil düstur yoxdur) — kod bazasında `PaymentTypes.Cash|Card|Credit` üzrə axtarışla təsdiqləndi. |
| AC5 | Summary/sales.pdf Dashboard və Gün-sonu ilə üst-üstə düşür (qarışıq gün) | ✅ | `SalesModuleContractTests.TC_Reports_Path_Matches_The_Day_End_Path_For_The_Same_Mixed_Day` eyni günün `GetDayTotalsAsync` (gün-sonu) və `GetSalesAsync`+`ComputeReceivedTotals` (summary/pdf) nəticələrini birbaşa müqayisə edir — 500/150/300 hər iki tərəfdə eyni. Əlavə olaraq `ReportsApiTests.Summary_Splits_A_Partially_Paid_Credit_Sale_Into_Received_Cash_And_Remaining_Debt` HTTP səviyyəsində `/api/reports/summary` və `/api/reports/dashboard`-ı müqayisə edir. |
| AC6 | sales.pdf uğurla generasiya olunur, reqressiya yoxdur | ✅ | `ExportSalesPdfHandlerTests.TC_Sales_Pdf_Export_Smoke_Test_For_A_Mixed_Day` — `Result.Success`, `%PDF` magic bytes, content-type `application/pdf`, fayl adı `satislar-{from}-{to}.pdf` formatında dəyişməz. |
| AC7 | Mövcud davranış (profit, expenses split, unknown-profit) reqressiyaya uğramır | ✅ | Diff yalnız `CashSales/CardSales/CreditSales`-in necə hesablandığına toxunur; mövcud `GetSummaryHandlerTests` (expense split, netProfit, unknown-profit) faylında heç bir mövcud test dəyişdirilməyib — yalnız yeni testlər əlavə olunub (git diff ilə təsdiqləndi), hamısı yaşıl. |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Qeyd |
|---|---|---|---|
| TC1 | Tam ödənilmiş Nağd(200)+Kart(150) → Cash=200,Card=150,Credit=0 | ✅ | `SalesReportRowTotalsTests` və `GetSummaryHandlerTests` daxilindəki qarışıq gün testlərinin alt-hissəsi olaraq örtülüb (tam sətirlər üzərindən). |
| TC2 | Qismən ödənilmiş Nisyə: Total=500,Paid=300,PaidVia=Nağd → Cash=300,Credit=200 | ✅ | `GetSummaryHandlerTests.BE19_Mixed_Day_Cash_Card_Credit_Is_The_Real_Received_Split`, `SalesReportRowTotalsTests.TC_Mixed_Day_Splits_Cash_Card_And_Credit_Correctly`, `ReportsApiTests.Summary_Splits_A_Partially_Paid_Credit_Sale_Into_Received_Cash_And_Remaining_Debt` (HTTP, delta əsaslı). |
| TC3 | Tam ödənilməmiş Nisyə: Total=100,Paid=0 → Credit=100, Cash/Card dəyişmir | ✅ | Eyni qarışıq gün testlərinin tərkib hissəsi (dördüncü sətir). |
| TC4 | Qarışıq gün: Nağd200+Kart150+Nisyə(500/300)+Nisyə(100/0) → Cash=500,Card=150,Credit=300, `GetDayTotalsAsync` ilə birbaşa müqayisə | ✅ | `SalesModuleContractTests.TC12_...` + `TC_Reports_Path_Matches_The_Day_End_Path_For_The_Same_Mixed_Day` — iki yol bir-birinə birbaşa `Assert.Equal` ilə tutuşdurulur. |
| TC5 | Nisyə qalığı Kartla ödənilib → Card-a düşür, Cash-ə yox | ✅ | `SalesReportRowTotalsTests.A_Credit_Rows_Card_Down_Payment_Counts_As_Card_Income_Not_Cash`. |
| TC6 | `PaidAmount`/`PaidVia` null (köhnə sətir) → köhnə fallback davranışı qorunur | ✅ | `SalesReportRowTotalsTests.Legacy_Rows_Without_PaidAmount_Fall_Back_To_The_Sales_TotalAmount_Rule`. |
| TC7 | Boş dövr → Cash=Card=Credit=0, exception yoxdur | ✅ | `SalesReportRowTotalsTests.No_Rows_Yields_All_Zeros`, `GetSummaryHandlerTests.BE19_A_Period_Without_Sales_Reports_Zero_Cash_Card_And_Credit`, `ExportSalesPdfHandlerTests.An_Empty_Period_Still_Produces_A_Valid_Pdf`. |
| TC8 | Qarışıq gün → sales.pdf smoke test | ✅ | `ExportSalesPdfHandlerTests.TC_Sales_Pdf_Export_Smoke_Test_For_A_Mixed_Day` (+ əlavə byte-diff testləri qismən ödəniş sətrinin faktiki render olunduğunu göstərir). |
| TC9 | `rangeFrom > rangeTo` → `Result.Failure "Exports.InvalidRange"` | ✅ | `ExportSalesPdfHandlerTests.An_Invalid_Range_Fails_Without_Touching_The_Sales_Module`. |
| TC10 | Mövcud `GetSummaryHandlerTests` (expense split, netProfit) dəyişməz yaşıl qalır | ✅ | Git diff təsdiqlədi ki, faylda heç bir mövcud test dəyişdirilməyib, yalnız 2 yeni test əlavə olunub; bütün mövcud testlər `dotnet test` icrasında yaşıl. |

## Tapılan buglar

Yoxdur. Bu QA sessiyasında funksional bug aşkar edilməyib.

## İcra olunan test əmrləri

```bash
git -C backend fetch origin
git -C backend checkout task/BE-19-reports-received-amount
git -C backend pull origin task/BE-19-reports-received-amount

dotnet build MayaPro.WarehouseApi.sln
# → Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test MayaPro.WarehouseApi.sln --no-build
# → 465/465 yaşıl, 0 uğursuz (DayEnd 4, Reports 22, Customers 6, SharedKernel 36,
#    Suppliers 12, Expenses 52, Sales 48, Exports 46, Products 71, Auth 4,
#    IntegrationTests 164)
```

Əlavə olaraq kod nəzərdən keçirmə ilə statik doğrulama aparıldı:
- `SalesReportRowTotals.ComputeReceivedTotals` (SharedKernel) ↔ `SalesModuleContract.GetDayTotalsAsync` (SQL) düsturunun sətir-sətir eyniliyi.
- `DashboardCalculator.ExpectedCash`-in artıq `ReceivedVia`/`ReceivedAmount` üzərindən işlədiyi (3-cü dublikat yoxdur) təsdiqləndi.
- `git diff main...task/BE-19-reports-received-amount -- tests/MayaPro.WarehouseApi.IntegrationTests/ReportsApiTests.cs` — yalnız əlavə (additive) dəyişiklik, mövcud test dəyişdirilməyib.

## Tövsiyələr

- Funksional tərəfdən blocker yoxdur, AC1–AC7 və TC1–TC10 tam örtülüb və PASS-dır.
- Senior review-də qeyd olunan `FormatMoney`-in thread culture-a bağlı olması (invariant/az-AZ keçidi) bu PR-ın əhatəsindən kənardır — ayrıca aşağı prioritetli backlog task kimi izlənilə bilər, blocker deyil.
- Reqressiya riski aşağıdır: dəyişiklik yalnız Cash/Card/Credit hesablama nöqtəsinə (ortaq helper) yönəlib, digər sahələr (profit, expenses split, unknown-profit, PDF sətir strukturu istisna olmaqla) toxunulmayıb və mövcud testlər dəyişdirilməyib.
