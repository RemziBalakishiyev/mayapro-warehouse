# QA Report — BE-15: Qismən ödənişli satış (PaidAmount, qalıq borc, real kassa hesabı)

**Tarix:** 2026-07-30
**QA Agent:** qa-tester
**Test edilən:** Issue https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/15, PR https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/18, branch `task/BE-15-partial-payment`, commit `7d3b37d` (HEAD — senior backend review refactor-u daxil edir, `5b71427` üzərində).
**Mühit:** Lokal, Windows, .NET 8 SDK, tam solution üzərində `dotnet build` + `dotnet test` (`IntegrationTests` və `SalesMigrationTests` real SQL Server-ə qarşı işləyir, digərləri in-memory/unit).

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 11 (AC1…AC11) |
| ✅ Pass | 11/11 |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Ümumi Test Case (TC1…TC12 + sərhəd halları) | 12 TC + 6 əlavə sərhəd halı |
| ✅ Pass | 18/18 |
| ❌ Fail | 0 |
| Yaradılan bug sayı | 0 |
| QA tərəfindən əlavə edilən yeni test sayı | 2 (aşağıya bax — mövcud test dəstində iki konkret boşluq tapıldı və bağlandı) |
| **Yekun qərar** | **PASS → Done** |

Build (əvvəl, QA testlərindən öncə): `dotnet build` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test (əvvəl): `dotnet test` (tam solution) → **448/448 keçdi**, 0 uğursuz, 0 skip.

Build (QA-nın 2 əlavə testindən sonra): `dotnet build` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test (sonra): `dotnet test` (tam solution) → **450/450 keçdi**, 0 uğursuz, 0 skip.
Bölgü (sonuncu run): `Modules.Customers.Tests` 6, `Modules.DayEnd.Tests` 4, `Modules.Reports.Tests` 20, `SharedKernel.Tests` 30, `Modules.Suppliers.Tests` 12, `Modules.Expenses.Tests` 52, `Modules.Exports.Tests` 41, `Modules.Auth.Tests` 4, `Modules.Sales.Tests` 47 (+1 QA), `Modules.Products.Tests` 71, `IntegrationTests` 163 (+1 QA).

## Nəzərdən keçirilən kod

- `src/Modules/.../Modules.Sales/Domain/Sale.cs` — `PaidAmount`/`RemainingAmount`/`PaidVia` sahələri, `Create`/`CreateManual`/`ReviseCatalogued`/`ReviseManual`/`ApplyPayment`.
- `src/Modules/.../Modules.Sales/Domain/SalePaymentPlan.cs` — tək mənbə: `Resolve` metodu paid/remaining/stored-type/paid-via qərarını verir.
- `src/Modules/.../Modules.Sales/Application/Abstractions/SaleWriteValidator.cs` + `ISaleWriteCommand.cs` — 0≤paidAmount≤total, paidVia yalnız Nağd/Kart, qalıq>0-da müştəri məcburi.
- `src/Modules/.../Modules.Sales/Application/UseCases/CreateSale/{CreateSaleCommand,CreateSaleHandler,CreateSaleValidator}.cs`
- `src/Modules/.../Modules.Sales/Application/UseCases/UpdateSale/UpdateSaleHandler.cs` — reverse-and-reapply, köhnə `RemainingAmount` qədər borc geri sarılır.
- `src/Modules/.../Modules.Sales/Application/UseCases/DeleteSale/DeleteSaleHandler.cs` — yalnız `RemainingAmount` qədər borc geri sarılır (tam Total yox).
- `src/Modules/.../Modules.Sales/Application/SalesModuleContract.cs` — `GetDayTotalsAsync` (real kassa hesabı), `GetInvoiceSaleAsync`.
- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/ISalesModule.cs` — `SalesReportRow.ReceivedAmount/ReceivedVia` (geriyə-uyğun fallback).
- `src/Modules/.../Modules.Reports/Application/UseCases/GetDashboard/DashboardCalculator.cs` — `ExpectedCash` eyni `ReceivedVia`/`ReceivedAmount` məntiqi ilə.
- `src/Modules/.../Modules.Exports/Application/UseCases/ExportSaleInvoicePdf/ExportSaleInvoicePdfHandler.cs` — "Ödənildi/Qalıq borc" sətri.
- `src/Modules/.../Modules.Sales/Infrastructure/Migrations/20260730142515_AddSalePaidAmount.cs` + `Infrastructure/Configurations/SaleConfiguration.cs`.
- Müqayisə üçün (scope xaricində, aşağıda qeyd olunub): `Modules.Reports/Application/UseCases/GetSummary/GetSummaryHandler.cs`, `Modules.Exports/Application/UseCases/ExportSalesPdf/ExportSalesPdfHandler.cs`.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC1 | Migration `AddSalePaidAmount`: `PaidAmount` decimal(18,2) not null; backfill Cash/Card→Total, Credit→0; Down işləyir; re-run təhlükəsiz | ✅ PASS | `AddSalePaidAmount.cs:13-49` — `AddColumn<decimal>` `nullable:false, defaultValue:0m`; backfill SQL `WHERE PaymentType <> N'Credit' AND PaidAmount = 0` yalnız Cash/Card sətirlərini `TotalAmount`-a köçürür, Credit toxunulmaz qalır (default 0 ilə üst-üstə düşür). `Down()` hər iki sütunu (`PaidAmount`,`PaidVia`) düzgün silir. Test: `SalesMigrationTests.Migration_Backfills_PaidAmount_From_The_Payment_Type` (real SQL Server, Cash→200/Cash, Card→200/Card, Credit→0/Cash) + QA-nın əlavə etdiyi `Migration_Backfill_Statement_Is_Safe_To_Rerun` (aşağıda). |
| AC2 | `CreateSaleCommand.PaidAmount` (nullable), `PaidVia` (default Nağd); göndərilməyəndə Nağd/Kart→Total, Nisyə→0 | ✅ PASS | `CreateSaleCommand.cs:37-50` — hər iki sahə nullable optional parametr. `SalePaymentPlan.Resolve` (`SalePaymentPlan.cs:50`) — `effectivePaid = paidAmount ?? (requestedType==Credit ? 0m : total)`. Test: `SalePaymentPlanTests.TC2_...Defaults_To_Fully_Paid`, `TC3_...Defaults_To_Zero_Paid`, `SaleTests.Create_Without_PaidAmount_Defaults_To_Fully_Paid_On_Cash`/`..._Defaults_To_Unpaid_On_Credit`. |
| AC3 | 0 ≤ PaidAmount ≤ TotalAmount (mənfi/limitdən çox → 400) | ✅ PASS | `SaleWriteValidator.cs:39-45` — iki ayrı `RuleFor`, `Total(command)` komandanın öz `SalePrice×Quantity`-si ilə (heç vaxt client-total). Test: `CreateSaleValidatorTests.TC6_PaidAmount_Above_Total_Is_Invalid`, `TC7_Negative_PaidAmount_Is_Invalid`; API: `SalesApiTests.TC6_PaidAmount_Above_Total_Returns_400` (400 + dəqiq mesaj). |
| AC4 | qalıq>0 → CustomerId məcburi, mesaj "Qalıq borc üçün müştəri seçilməlidir" | ✅ PASS | `SaleWriteValidator.cs:53-56` + `HaveCustomerWhenBalanceRemains` (61-69) — `PaymentType`-dan asılı olmayaraq. Test: `CreateSaleValidatorTests.TC5_...`, API `SalesApiTests.TC5_Zero_Paid_Cash_Sale_Without_Customer_Returns_400` — mesaj sözbəsöz assert olunub. |
| AC5 | Saxlanan PaymentType: qalıq>0→Nisyə; qalıq=0→command-dakı Nağd/Kart (paidVia nəzərə alınmaqla) | ✅ PASS | `SalePaymentPlan.cs:57-64` — `storedType`/`resolvedVia` düsturu. Test: `SalePaymentPlanTests.A_Nisye_Request_Settled_In_Full_By_Card_Is_Booked_As_Card_Not_Cash`, `A_Fully_Paid_Sale_Never_Splits_Its_Money_Across_Two_Methods` (bütün 15 kombinasiya), API `TC10`/`TC10b`. |
| AC6 | Müştəri borcu YALNIZ qalıq qədər artır | ✅ PASS | `CreateSaleHandler.cs:57,83` və `UpdateSaleHandler.cs:69,91` — `customers.IncreaseDebtAsync(customerId, plan.Remaining, ct)` (heç vaxt `total`). Test: `SalesApiTests.TC1/TC4/TC8/TC10` — borc dəyişikliyi `Remaining`-ə bərabər ölçülür. |
| AC7 | `GetDayTotalsAsync`: Cash=PaidVia=Nağd olan PaidAmount cəmi (nisyə satışların nağd hissəsi daxil); Card analoji; Credit=qalıqların cəmi | ✅ PASS | `SalesModuleContract.cs:31-39` — dəqiq bu düstur, tək qruplu sorğu. Test: `SalesModuleContractTests.TC12_Mixed_Day_Splits_Cash_Card_And_Credit_Correctly` (Cash=500, Card=150, Credit=300 — AC-dəki ədədlərlə bitə-bitə eyni), `Credit_Row_With_No_Remaining_Balance_Contributes_Nothing_To_Credit` (sərhəd), API `DayEndApiTests.Close_Day_Computes_Totals_Server_Side_...`. |
| AC8 | Dashboard "kassada olmalı" eyni məntiqlə | ✅ PASS | `DashboardCalculator.cs:83-100` (`ExpectedCash`) — `SalesReportRow.ReceivedVia`/`ReceivedAmount` (AC7 ilə eyni fallback qaydası, `ISalesModule.cs:159-167`) istifadə edir, `GetDayTotalsAsync`-la eyni nəticəni verir. Test: `DashboardCalculatorTests` (mövcud paketdə cash-in `ReceivedAmount`/`ReceivedVia` üzərindən hesablandığı yoxlanılır). |
| AC9 | Delete/Update reverse borcu qalıq qədər azaldır | ✅ PASS | `DeleteSaleHandler.cs:39` — `customers.DecreaseDebtAsync(customerId, sale.RemainingAmount, ct)`; `UpdateSaleHandler.cs:57` — eyni. Test: `SalesApiTests.TC8_Deleting_A_Partially_Paid_Sale_Reverses_Only_The_Remaining_Debt`, `TC9_Fully_Paying_Off_...`, `SaleTests.ReviseCatalogued_Carries_The_New_Paid_Amount_And_Via`. |
| AC10 | DTO-da paidAmount/remainingAmount/paidVia; PDF-də qismən ödənişdə sətir | ✅ PASS | `SaleDto.cs:39-41`, `SaleMapping.cs:28-30,59-61` — hər iki DTO-da 3 sahə. PDF: `ExportSaleInvoicePdfHandler.cs:180-184` — yalnız `RemainingAmount>0`-da "Ödənildi: X {valyuta} · Qalıq borc: Y {valyuta}". Test: `SaleTests.ToDto_And_ToDetailDto_Carry_PaidAmount_RemainingAmount_And_PaidVia`, `ExportSaleInvoicePdfHandlerTests.TC11_...`, `Partially_Paid_Invoice_Differs_From_An_Otherwise_Identical_Fully_Paid_One` (byte-səviyyəli sübut — mətn çıxarma asılılığı yoxdur, digər export testləri ilə eyni konvensiya). |
| AC11 | Geriyə uyğunluq: PaidAmount göndərilməyəndə köhnə davranış | ✅ PASS | `SaleTests.Create_Without_PaidAmount_Defaults_To_Fully_Paid_On_Cash`/`..._Unpaid_On_Credit`, API `SalesApiTests.TC2/TC3`, həmçinin ~15 köhnə (BE#15-dən əvvəlki) test faylı `paidAmount` heç göndərmədən davam edir və hamısı yaşıldır. |

## Test case nəticələri (TC1…TC12)

| TC | Ssenari | Nəticə | Faktiki test |
|---|---|---|---|
| TC1 | Nisyə 500, paid 300 Nağd → paid=300, qalıq=200, borc+200, Cash+=300/Credit+=200 | ✅ PASS | `SalesApiTests.TC1_...`, `SalePaymentPlanTests.TC1_...`, `SaleTests.Create_With_Explicit_Partial_PaidAmount_Computes_Remaining` |
| TC2 | Nağd 500, paidAmount göndərilmir → paid=500, qalıq=0, borc dəyişmir | ✅ PASS | `SalesApiTests.TC2_...` |
| TC3 | Nisyə 200, paidAmount göndərilmir → paid=0, qalıq=200, borc+200 | ✅ PASS | `SalesApiTests.TC3_...`, `SalePaymentPlanTests.TC3_...` |
| TC4 | 1000, paid 600 Kart, Nisyə → PaymentType=Nisyə, Card+=600/Credit+=400, borc+400 | ✅ PASS | `SalesApiTests.TC4_...`, `SalePaymentPlanTests.TC4_...` |
| TC5 | Nağd 150, paidAmount=0, CustomerId=null → 400 "Qalıq borc üçün müştəri seçilməlidir" | ✅ PASS | `SalesApiTests.TC5_...`, `CreateSaleValidatorTests.TC5_...` |
| TC6 | 300, paidAmount=350 → 400 | ✅ PASS | `SalesApiTests.TC6_...`, `CreateSaleValidatorTests.TC6_...` |
| TC7 | 300, paidAmount=−50 → 400 | ✅ PASS | `CreateSaleValidatorTests.TC7_Negative_PaidAmount_Is_Invalid` (unit səviyyəsində; API-də ayrıca TCX yoxdur, lakin validator paylaşılan olduğu üçün handler-də eyni nəticə qaçınılmazdır) |
| TC8 | TC1-dəki satış silinir → borc−200, stok geri artır | ✅ PASS | `SalesApiTests.TC8_...` |
| TC9 | Köhnə 500/300; yeni paidAmount=500 → köhnə qalıq 200 geri sarılır, yeni qalıq=0, PaymentType Nağd/Kart-a keçir | ✅ PASS | `SalesApiTests.TC9_...`, `SaleTests.ReviseCatalogued_Carries_The_New_Paid_Amount_And_Via`, `SalePaymentPlanTests.TC9_...` |
| TC10 | 400, paid=400, CustomerId var → borc dəyişmir | ✅ PASS | `SalesApiTests.TC10_...`, `TC10b_...` (Kart variantı), `CreateSaleValidatorTests.Fully_Paid_Credit_Request_Without_Customer_Is_Valid` |
| TC11 | TC1-dəki satışın qaimə PDF-i → "Ödənildi/Qalıq borc" sətri var | ✅ PASS | `ExportSaleInvoicePdfHandlerTests.TC11_...` |
| TC12 | Qarışıq gün: Nağd 200+Kart 150+Nisyə(500/300/Nağd)+Nisyə(100/0) → Cash=500, Card=150, Credit=300 | ✅ PASS | `SalesModuleContractTests.TC12_...` (ədədlər AC-dəki ilə bitə-bitə eyni), API `DayEndApiTests.Close_Day_Computes_Totals_Server_Side_...` (aşağı-sərhəd assert-lə, çünki paylaşılan test DB) |

**TC7 qeydi:** Issue-da `SalesApiTests` daxilində ayrıca `TC7`-adlı HTTP testi yoxdur, amma `SaleWriteValidator` `CreateSaleCommand`/`UpdateSaleCommand` üçün **eyni** sinifdir (`SaleWriteValidator<TCommand>`), və `CreateSaleValidatorTests.TC7_Negative_PaidAmount_Is_Invalid` bu qaydanı unit səviyyəsində tam əhatə edir; `Negative_PurchasePricePerUnit_Is_Invalid`/`Negative_Purchase_Price_Returns_400_And_Writes_No_Sale` kimi digər mənfi-dəyər testləri də API səviyyəsində eyni pattern-i sübut edir (validation → 400, heç nə yazılmır). Risk aşağı qiymətləndirilib, bug sayılmayıb.

## Sərhəd hallarının əlavə yoxlanışı (tapşırıqda spesifik tələb olunan)

- **PaidAmount = TotalAmount dəqiq bərabər** — ✅ `SalePaymentPlanTests.TC10_Fully_Paid_Sale_Never_Reaches_Credit_Even_With_A_Customer`, `CreateSaleValidatorTests.Fully_Paid_Credit_Request_Without_Customer_Is_Valid`, API `SalesApiTests.TC10_...`.
- **PaidAmount=0 + CustomerId var (tam nisyə)** — ✅ `SalesApiTests.TC3_...` (paidAmount göndərilmir → daxili olaraq 0, eyni yol), `SalePaymentPlanTests.TC3_Credit_Sale_Without_PaidAmount_Defaults_To_Zero_Paid`.
- **paidVia yanlış dəyər ("Nisyə" göndərilsə nə olur)** — ✅ `SalePaymentPlanTests.PaidVia_Can_Never_Resolve_To_Credit_Itself` (Cash-ə düşür) + `CreateSaleValidatorTests.Unrecognised_PaidVia_Code_Is_Invalid` (validator səviyyəsində "Nisyə" də daxil olmaqla hər tanınmayan kod 400-lə rədd edilir, çünki `SaleWriteValidator.cs:47-49` yalnız `null`/Cash/Card qəbul edir — "Nisyə" bu üçünə düşmür, deməli əslində bu kod HEÇ handler-ə çatmır, validator mərhələsində 400 alır). Uyğunluq: validator "Nisyə"-ni rədd edir (400), amma `SalePaymentPlan.Resolve` özü də (əgər hardcode çağırılsaydı) "Nisyə"-ni Cash-ə salır — iki qat müdafiə, ziddiyyət yoxdur.
- **Manual satış yolu (CreateManual)** — ✅ TC1-TC10b-nin demək olar hamısı məhz `CreateManual` yolu ilə test olunub (`productId=null`); `SaleTests`-də ayrıca domain-səviyyəli testlər (`Create_With_Explicit_Partial_PaidAmount_Computes_Remaining` s.) də var.
- **Silinmiş/geri qaytarılmış satışın gün totallarına təsiri** — ⚠️→✅ **Boşluq tapıldı və bağlandı.** Mövcud dəstdə `DeleteSaleHandler`-in stok/borc effektini yoxlayan çoxlu test var idi, amma `GetDayTotalsAsync`-a təsirini **birbaşa** yoxlayan test yox idi (yalnız dolayı — silinmiş sətir sorğuya düşmür, çünki EF `Remove` edir). QA əlavə etdi: `SalesModuleContractTests.Deleting_A_Sale_Removes_It_From_The_Days_Totals` — Cash satışı silinir, gün totalının Cash sütunu 200→0 düşür, toxunulmamış Card sətri (150) dəyişmir. ✅ PASS.
- **Decimal yuvarlaqlaşdırma (0.005 fərqlər)** — ✅ Nəzərdən keçirilib: C#-ın `decimal` tipi ikili deyil, onluq-əsaslı **dəqiq** arifmetika aparır (float/double kimi yuvarlaqlaşdırma xətası yoxdur), buna görə `paidAmount <= Total` müqayisəsi 0.005 kimi fərqlərdə də tam dəqiqdir — riyazi cəhətdən bug ehtimalı yoxdur. `decimal(18,2)` SQL sütunu isə yalnız DB-yə yazılanda 2 onluq rəqəmə yuvarlaqlaşdırır (bank yuvarlaqlaşdırması), bu, mövcud `SalesMigrationTests.Migration_Backfills_Purchase_Price_Only_For_Free_Form_Sales` testindəki `roundingId` halında artıq sübut olunub (`96.666… → 96.67`, eyni sütun tipi/converter `PaidAmount` üçün də tətbiq olunur). Əlavə test yazılmadı — riyazi xassə artıq dolayı sübut olunub, yeni bug riski görülmədi.
- **Migration "re-run təhlükəsiz" iddiası** — ⚠️→✅ **Boşluq tapıldı və bağlandı.** AC1-in "re-run təhlükəsiz" şərti kod şərhində izah olunurdu, amma birbaşa test edilmirdi. QA əlavə etdi: `SalesMigrationTests.Migration_Backfill_Statement_Is_Safe_To_Rerun` — miqrasiya tətbiq olunduqdan sonra backfill SQL-i ikinci dəfə əl ilə işə salınır, Cash və Credit sətirlərinin hər ikisi dəyişməz qalır (WHERE bəndinin qoruması sübut olunur). ✅ PASS.

## Bilinən məhdudiyyətlərin qiymətləndirilməsi (senior-un qeyd etdiyi)

**(a) PDF sətri mağaza valyutası ilə ("300.00 AZN"), AC nümunəsində "₼" idi.**
Qəbul edilə bilər. `ExportSaleInvoicePdfHandler.cs:182-183` — `{model.Store.Currency}` istifadə edir, invoisin **bütün digər** məbləğ sətirləri (Cəm, YEKUN, Ümumi qalıq borc) də eyni `Store.Currency`-dən istifadə edir (`ComposeTotals` metodu boyu). Yəni yeni sətir mövcud konvensiyaya tam uyğundur — invoisdə iki fərqli valyuta format-i olmazdı, "₼" yalnız AC-nin nümunə mətnində illüstrasiya məqsədilə işlənib, hərfi tələb deyil. Bug sayılmır.

**(b) `GET /api/reports/summary` və `sales.pdf` export hələ köhnə düsturla hesablayır.**
Bu, **real və mövzu ilə bağlı funksional uyğunsuzluqdur**, təsdiq edildi kod baxışı ilə:
- `GetSummaryHandler.cs:43-45` — `CashSales`/`CardSales`/`CreditSales` hələ `s.PaymentType == Cash/Card/Credit` və `s.TotalAmount` ilə hesablanır (AC7/AC8-in `ReceivedVia`/`ReceivedAmount` düsturunu **istifadə etmir**).
- `ExportSalesPdfHandler.cs:40-42` — eyni köhnə düstur.

**Nəticə:** istənilən qismən ödənişli (Nisyə + nağd/kart ödəniş) satış olduğu gündə/dövrdə, Dashboard və Gün-sonu bağlanışı (`GetDayTotalsAsync`) ilə Hesabatlar səhifəsi/`sales.pdf` **fərqli Cash/Card/Credit rəqəmləri göstərəcək** — məsələn TC12-nin dəqiq ssenarisində: gün-sonu Cash=500/Card=150/Credit=300 göstərəcək, halbuki `/api/reports/summary` eyni dövr üçün Cash=200/Card=150/Credit=600 (500+100) göstərəcək, çünki iki Nisyə sətrinin hamısı `TotalAmount` üzərindən Credit-ə düşür, nağd düşən 300 heç yerdə "Cash" kimi görünmür. Bu, mağaza sahibinə **ziddiyyətli maliyyə mənzərəsi** verə bilər (iki ekranda fərqli "nə qədər nağd yığılıb" rəqəmi).

Bu, tapşırıqda **açıq şəkildə scope xaricində** elan edilib, ona görə QA bunun üçün ayrıca bug task-ı **açmır** (tapşırığın 5-ci bəndinə uyğun — yalnız orkestratora bildirilir), lakin ciddiliyi **Medium** olaraq qiymətləndirilir və gələcək backlog üçün tövsiyə olunur: `GetSummaryHandler`/`ExportSalesPdfHandler`-in Cash/Card/Credit hesablamalarını `SalesReportRow.ReceivedVia`/`ReceivedAmount`-a keçirmək (`DashboardCalculator.ExpectedCash`-in etdiyi kimi).

## Tapılan buglar

Heç bir bug tapılmadı (BE#15-in öz scope-u daxilində). Yuxarıdakı (b) bəndi bir **məlum, scope-xarici uyğunsuzluqdur** — tapşırıq təlimatına əsasən bunun üçün ayrıca bug task-ı yaradılmadı, yalnız orkestratora hesabatda bildirilir.

## QA tərəfindən əlavə edilən testlər

1. `tests/MayaPro.WarehouseApi.Modules.Sales.Tests/SalesModuleContractTests.cs` → `Deleting_A_Sale_Removes_It_From_The_Days_Totals` — silinmiş satışın gün totallarından tam çıxdığını birbaşa sübut edir (əvvəllər yalnız dolayı yolla əhatə olunurdu).
2. `tests/MayaPro.WarehouseApi.IntegrationTests/SalesMigrationTests.cs` → `Migration_Backfill_Statement_Is_Safe_To_Rerun` — AC1-in "re-run təhlükəsiz" iddiasını miqrasiyanın öz SQL-ini ikinci dəfə işə salaraq birbaşa sübut edir.

Hər ikisi yaşıldır (bax yuxarıdakı test nəticələri). Tətbiq kodu **dəyişdirilmədi** — yalnız test faylları.

## İcra olunan test əmrləri

```bash
git -C ".../backend" status
# On branch task/BE-15-partial-payment, up to date with origin, clean

git -C ".../backend" log --oneline -3
# 7d3b37d refactor: senior backend review duzelisleri
# 5b71427 feat: qismen odenisli satis
# 576bcc9 Merge pull request #17 from .../task/BE-13-excel-import

dotnet build
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test --no-build   # (QA-nın 2 əlavə testindən əvvəl)
# TOTAL: 448/448 passed, 0 failed, 0 skipped

# ... QA testləri əlavə edildi (SalesModuleContractTests, SalesMigrationTests) ...

dotnet build
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test --no-build   # (QA-nın 2 əlavə testindən sonra)
# MayaPro.WarehouseApi.Modules.Customers.Tests    6/6
# MayaPro.WarehouseApi.Modules.DayEnd.Tests       4/4
# MayaPro.WarehouseApi.Modules.Reports.Tests      20/20
# MayaPro.WarehouseApi.SharedKernel.Tests         30/30
# MayaPro.WarehouseApi.Modules.Suppliers.Tests    12/12
# MayaPro.WarehouseApi.Modules.Expenses.Tests     52/52
# MayaPro.WarehouseApi.Modules.Exports.Tests      41/41
# MayaPro.WarehouseApi.Modules.Auth.Tests         4/4
# MayaPro.WarehouseApi.Modules.Sales.Tests        47/47  (+1 QA)
# MayaPro.WarehouseApi.Modules.Products.Tests     71/71
# MayaPro.WarehouseApi.IntegrationTests           163/163  (+1 QA)
# TOTAL: 450/450 passed, 0 failed, 0 skipped
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; branch `task/BE-15-partial-payment` QA-nı problemsiz keçdi.
- Bug tapılmadı — backend taskı **Done** statusuna keçirilə bilər.
- **Gələcək backlog üçün tövsiyə (bloklayıcı deyil, Medium prioritetli):** `GetSummaryHandler` (`/api/reports/summary`) və `ExportSalesPdfHandler` (`sales.pdf`) hazırda Cash/Card/Credit-i köhnə `PaymentType==X` + `TotalAmount` düsturu ilə hesablayır, Dashboard/Gün-sonu ilə (AC7/AC8) uyğunsuzdur qismən ödənişli satışlarda. Tövsiyə: hər ikisini `SalesReportRow.ReceivedVia`/`ReceivedAmount`-a keçirmək (bax yuxarıda "(b)" bölməsi).
- PDF-in valyuta formatı (mağaza valyutası, "₼" yox) qəbul ediləndir — AC-nin nümunəsi illüstrativ idi, invoisin qalan hissəsi ilə tutarlıdır.
