# QA Report — BE#21: Açıq borclar siyahısı (FIFO bölgü) — `GET /api/customers/open-debts`

**Tarix:** 2026-08-01
**QA Agent:** qa-tester
**Test edilən PR:** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/24 (branch `task/BE21-open-debts-fifo`, commit `20480a2`)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/21
**Mühit:** Lokal, Windows 11, .NET 8 SDK, SQL Server (`MayaProWarehouse_Test`), `dotnet build` / `dotnet test` (tam solution, 11 test layihəsi)

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Acceptance Criteria | 8 (AC1–AC8) — 8 ✅ / 0 ❌ |
| Test case | 8 (TC1–TC8) — 8 ✅ / 0 ❌ / 0 ⚠️ |
| Yaradılan bug sayı | 0 |
| Test örtüyü GAP-ları | 2 (TC4 sərhəd, TC7 — davranış düzgündür, commit olunmuş test yoxdur) |
| Qeydlər (bloklamayan) | 3 (OBS-1…OBS-3) |
| **Yekun qərar** | **QA PASSED → Done** |

## Build / Test rəqəmləri (bu QA sessiyasında bilavasitə icra edilib)

```
dotnet build MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be21/
# → Build succeeded.
#     0 Warning(s)
#     0 Error(s)
#   Time Elapsed 00:00:04.43
```

```
dotnet test MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be21/
# Passed! - Failed: 0, Passed:  36, Total:  36 - SharedKernel.Tests.dll
# Passed! - Failed: 0, Passed:  22, Total:  22 - Modules.Reports.Tests.dll
# Passed! - Failed: 0, Passed:   4, Total:   4 - Modules.DayEnd.Tests.dll
# Passed! - Failed: 0, Passed:  52, Total:  52 - Modules.Expenses.Tests.dll
# Passed! - Failed: 0, Passed:  50, Total:  50 - Modules.Sales.Tests.dll
# Passed! - Failed: 0, Passed:  12, Total:  12 - Modules.Suppliers.Tests.dll
# Passed! - Failed: 0, Passed:   4, Total:   4 - Modules.Auth.Tests.dll
# Passed! - Failed: 0, Passed:  46, Total:  46 - Modules.Exports.Tests.dll
# Passed! - Failed: 0, Passed:  71, Total:  71 - Modules.Products.Tests.dll
# Passed! - Failed: 0, Passed:  16, Total:  16 - Modules.Customers.Tests.dll
# Passed! - Failed: 0, Passed: 166, Total: 166 - IntegrationTests.dll
```

- **Cəm: 479/479 yaşıl, 0 uğursuz, 0 skipped.**
- BE#19 QA sessiyasındakı baza 465 idi → bu PR **+14 test** gətirir: Customers 6→16 (`GetOpenDebtsHandlerTests` — 10 test), Sales 48→50 (`SalesModuleContractTests` — 2 test), IntegrationTests 164→166 (`CustomersApiTests` — 2 e2e test).
- İnteqrasiya testləri real host + real SQL Server üzərində işləyir, yəni `GetOutstandingSalesAsync`-in SQL-ə tərcüməsi (client-side evaluation yoxdur) və auth pipeline faktiki olaraq yoxlanılıb.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Faktiki yoxlama |
|---|---|---|---|
| AC1 | Endpoint mövcuddur, sətirdə 10 sahə (customerId…daysOld) | ✅ | `CustomersEndpoints.cs:46` — `group.MapGet("/open-debts", …)`, `WithName("GetOpenDebts")`. `OpenDebtDto` bütün 10 sahəni daşıyır. `source` dəyərləri `CustomerHistoryEntryType.Sale`/`InitialDebt` sabitlərindəndir → wire-də `"sale"` / `"initialDebt"` (tarixçə feed-i ilə eyni lüğət). `description`: satışda `"{ProductName} × {Quantity}"`, ilkin borcda `"İlkin borc"`. HTTP səviyyəsində `CustomersApiTests` (satış sətri `"{product.Name} × 2"`, ilkin borc sətri `"İlkin borc"`) və QA probe-ları ilə təsdiqləndi. |
| AC2 | FIFO — ödənişlər ən köhnə mənbədən silinir | ✅ | `GetOpenDebtsHandler.cs:71` mənbələri `OrderBy(Date).ThenBy(SourceId)` ilə düzür, `:87-109` sətirlərində `Math.Min(unallocated, source.Amount)` ilə ardıcıl silir. Unit (`Payments_Are_Written_Off_Against_The_Oldest_Source_First`, `Initial_Debt_Is_Paid_Down_Before_Later_Sales`) + e2e (`Open_Debts_Write_A_Payment_Off_Against_The_Oldest_Source_First`). |
| AC3 | `remaining = 0` mənbələr siyahıya düşmür | ✅ | `GetOpenDebtsHandler.cs:94` — `if (remaining <= 0m) continue;`. `Fully_Paid_Sources_Are_Excluded` + e2e `Open_Debts_Drop_A_Fully_Paid_Source_And_List_The_Opening_Balance`. QA probe: borcu tam bağlanan müştərinin BÜTÜN sətirləri itir. |
| AC4 | Σ remaining = `Customer.Debt`; uyğunsuzluqda warning (exception yox) | ✅ | `WarnIfDebtDoesNotMatch` (`:138-148`) hər müştəri üçün çağırılır (mənbəsiz müştəri daxil, `:80`), `LogWarning` yazır, sorğu davam edir. **Real host log-unda faktiki müşahidə olunub** (QA probe icrası): `WRN Open debts mismatch for customer cbb428c3-…: sources remain 0 but stored debt is 440.00` — cavab yenə də `200 OK`, 16.6 ms. Bax: OBS-1. |
| AC5 | Tək sorğu dəsti, N+1 yoxdur | ✅ | Handler-də cəmi 4 sorğu var (customers, adjustments, `GroupBy` ödəniş cəmləri, `ISalesModule.GetOutstandingSalesAsync`) və müştəri döngüsünün içində HEÇ BİR `await` yoxdur (`:76-112`) — struktur olaraq N+1 mümkün deyil. **Ölçmə (QA probe, real SQL Server):** 3 müştəri / 6 sətir → **13 ms**, 43 müştəri / 86 sətir → **17 ms** (müştəri sayı 14×, gecikmə +4 ms) → sabit sorğu sayı təsdiqləndi. |
| AC6 | Sıralama `daysOld` DESC (ən köhnə əvvəldə) | ✅ | `:117-121` — `OrderBy(SourceDate).ThenBy(CustomerName).ThenBy(CustomerId)`; `sourceDate` artan = `daysOld` azalan. QA probe bütün cavab üzərində `Items[i-1].SourceDate <= Items[i].SourceDate` VƏ `Items[i-1].DaysOld >= Items[i].DaysOld` invariantını sətir-sətir yoxladı. Unit: `Rows_Of_All_Customers_Are_Ordered_Oldest_First_And_Summed`. |
| AC7 | Cavabda `totalRemaining` var | ✅ | `OpenDebtsDto(Items, TotalRemaining)`; `:123` — `ordered.Sum(r => r.Remaining)`. e2e-də `Assert.Equal(items.Sum(Remaining), TotalRemaining)`. |
| AC8 | Digər customers endpoint-ləri ilə eyni authorization | ✅ | Endpoint `/api/customers` qrupundadır (`RequireAuthorization()`, əlavə rol siyasəti yoxdur — `GET /api/customers`, `/{id}/payments`, `/{id}/history` ilə eyni). **QA probe (HTTP):** anonim `GET /api/customers/open-debts` → **401 Unauthorized** (`GET /api/customers` ilə eyni status), `Satıcı` rolu → **200 OK** (yenə `GET /api/customers` ilə eyni). Route toqquşması yoxdur: literal `/open-debts` seqmenti `{id:guid}` route-larına düşmür (real host-da 200 qaytarır). |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Sübut |
|---|---|---|---|
| TC1 | Müştəri A: qalıqlı 200 + 300 satış, 150 ödəniş → 50 və 300 | ✅ | Unit: `GetOpenDebtsHandlerTests.Payments_Are_Written_Off_Against_The_Oldest_Source_First` (originalAmount 200/paidSoFar 150/remaining 50; 300/0/300). e2e: `CustomersApiTests.Open_Debts_Write_A_Payment_Off_Against_The_Oldest_Source_First` — eyni rəqəmlər real SQL Server üzərində. |
| TC2 | Tam ödənilmiş mənbə siyahıda yoxdur | ✅ | `Fully_Paid_Sources_Are_Excluded` (200-lük mənbə 200 ödənişdən sonra itir) + e2e `Open_Debts_Drop_A_Fully_Paid_Source_And_List_The_Opening_Balance` (ödənişdən əvvəl 2 sətir → sonra 1 sətir). QA probe: 45 borcun 45-i ödənildikdə müştərinin sətirləri tamamilə yox olur, `Debt = 0`. |
| TC3 | Σ remaining = müştərinin `Debt`-i | ✅ | `Remaining_Sum_Equals_The_Customers_Debt_And_Logs_No_Warning` (100 ilkin + 200 + 300 − 150 = 450 = `customer.Debt`, warning YOXDUR). e2e-də hər iki testdə `Assert.Equal(GetCustomerAsync(id).Debt, rows.Sum(Remaining))`. QA probe əlavə olaraq: qismən ödənişli satış (500/200 avans), nisyə satışın silinməsi (`DELETE /credits/{saleId}`) və 43 müştərilik miqyas ssenarisində hər müştəri üçün bərabərlik saxlanılır. |
| TC4 | `daysOld` sərhədi: bugünkü borc → `daysOld = 0` | ✅ | e2e `Open_Debts_Write_A_Payment_Off_Against_The_Oldest_Source_First` → `Assert.Equal(0, rows[0].DaysOld)` (bugün yaradılmış satış). QA probe (unit səviyyəsi) əlavə olaraq: bugünkü ilkin borc → 0, VƏ gələcək tarixli mənbə → `Math.Max(0, …)` sayəsində mənfi deyil, 0. Bax GAP-1. |
| TC5 | `totalRemaining` = bütün sətirlərin cəmi | ✅ | e2e `Assert.Equal(openDebts.Items.Sum(r => r.Remaining), openDebts.TotalRemaining)`; unit `Rows_Of_All_Customers_…_And_Summed` (100 + 50 = 150). QA probe: 86 sətirlik cavabda və onluq kəsrli məbləğlərdə (75.55 + 24.45 + 150 = 200.00) bərabərlik dəqiqdir. |
| TC6 | Eyni tarixli mənbələr üçün deterministik sıra (Id tie-break) | ✅ | İki səviyyədə: (a) `SalesModuleContract.GetOutstandingSalesAsync` SQL-də `.OrderBy(Date).ThenBy(Id)` — `SalesModuleContractTests.GetOutstandingSales_Breaks_Ties_On_The_Same_Date_By_Id`; (b) handler mənbələri yenidən `OrderBy(Date).ThenBy(SourceId)` ilə düzür — `Sources_Tied_On_The_Same_Instant_Are_Allocated_In_A_Fixed_Deterministic_Order` (mənbələr TƏRS sırada verilir, bölgü yenə eyni: 80 kiçik Id-li sətrə düşür). QA probe: eyni anlıq ilkin borc + satış üçün ardıcıl iki sorğu eyni sıranı qaytarır. |
| TC7 | Ödəniş borcdan çox olarsa mənfi `remaining` yaranmır | ✅ | İki müdafiə xətti: (a) yazı anında `Customer.DecreaseDebt` ödənişin borcdan çox olmasına icazə vermir (`Payment_Exceeding_Debt_Returns_400_And_Leaves_Debt_Untouched`); (b) oxu anında `Math.Min(unallocated, source.Amount)` + `remaining <= 0 → continue`. QA probe (legacy/korlanmış data simulyasiyası): mənbələr 300, ödənişlər 400 → siyahı boş, `totalRemaining = 0`, heç bir mənfi sətir yoxdur; 250 ödəniş / 300 mənbə → yalnız 50-lik müsbət qalıq. Bax GAP-2. |
| TC8 | Borcu olmayan müştəri siyahıda yoxdur / boş siyahı düzgün | ✅ | `Customer_Without_Sources_Is_Absent_From_The_List` (boş `Items`, `TotalRemaining = 0`, warning yoxdur) + `Sources_Of_A_Deleted_Customer_Are_Ignored`. QA probe (HTTP): borcsuz müştəri cavabda yoxdur; tamamilə boş baza → `{ items: [], totalRemaining: 0 }`, exception yoxdur. |

## Test örtüyü GAP-ları (davranış düzgündür, commit olunmuş test yoxdur)

| # | GAP | Risk | Qeyd |
|---|---|---|---|
| GAP-1 | TC4 sərhəd halı yalnız inteqrasiya testində (`DaysOld == 0`) örtülüb; `GetOpenDebtsHandlerTests`-də `daysOld = 0` və gələcək tarixli mənbənin sıfıra "floor" edilməsi üçün ayrıca unit test yoxdur (mövcud unit test yalnız 31 günlük halı yoxlayır). | Aşağı | QA probe-u ilə hər iki hal faktiki icra edilib və PASS-dır. Gələcəkdə `DaysOld` düsturuna toxunulsa, reqressiya yalnız inteqrasiya testində tutulacaq. |
| GAP-2 | TC7 (bütün mənbələrdən çox ödəniş → mənfi `remaining` olmamalı) üçün commit olunmuş test yoxdur. | Aşağı | Bu hal normal axında `DecreaseDebt` sayəsində əlçatmazdır (yalnız legacy/miqrasiya datası ilə mümkündür), ona görə bloklamır. QA probe-u ilə yoxlanılıb: mənfi sətir yaranmır. |

Tövsiyə: hər iki GAP kiçik, əlavə (additive) unit test ilə bağlana bilər — bu PR üçün blocker deyil.

## Müşahidələr (bloklamayan)

| # | Müşahidə | Severity | Detal |
|---|---|---|---|
| OBS-1 | Seed/demo datasında 3 müştərinin borcu heç bir mənbəyə bağlı deyil → hər sorğuda 3 warning və 2150 AZN borc açıq borclar siyahısında görünmür | Low | `CustomerSeeder.cs:19-22` müştəriləri `Customer.Create(..., debt: 440 / 1250 / 460)` ilə yaradır, lakin uyğun `CustomerDebtAdjustment` (ilkin borc) sətri yazmır. Nəticə: `GET /api/customers/open-debts` hər dəfə 3 `WRN Open debts mismatch…` yazır, `totalRemaining` isə `GET /api/customers` və dashboard `TotalCustomerDebt` ilə 2150 AZN fərqlənir. **Bu, AC4-ün tam olaraq nəzərdə tutduğu davranışdır (exception yox, warning) və BE#21 kodunun deyil, seeder-in data məsələsidir.** Ayrıca kiçik task kimi seeder-ə ilkin borc sətirlərinin əlavə edilməsi tövsiyə olunur. Real müştəri axınında (`CreateCustomerHandler`) ilkin borc həmişə `CustomerDebtAdjustment` ilə birlikdə, tək tranzaksiyada yazılır — orada uyğunsuzluq yaranmır. |
| OBS-2 | Sətirdə mənbənin Id-si (`saleId`/`adjustmentId`) yoxdur | Low | `DebtSource.SourceId` yalnız daxili tie-break üçündür, cavaba çıxmır (AC1 tələb etmir). Frontend "Açıq borclar" ekranından sətri satışa bağlamaq (məs. mövcud `DELETE /api/customers/{id}/credits/{saleId}` əməliyyatı) istəyərsə, ayrıca sahə tələb olunacaq — FE taskı planlaşdırılarkən nəzərə alınmalıdır. |
| OBS-3 | Uyğunsuzluq warning-i hər sorğuda, hər müştəri üçün ayrıca yazılır | Info | Legacy drift olan böyük bazada log səs-küyü yarada bilər (məs. 100 uyğunsuz müştəri = sorğu başına 100 sətir). Gələcəkdə aqreqasiya (bir yekun warning) düşünülə bilər; funksional defekt deyil. |

## FIFO məntiqinin analitik yoxlanışı (kod nəzərdən keçirmə)

Handler ödənişləri bir-bir deyil, **cəm** kimi tətbiq edir (qruplaşdırılmış tək sorğu üçün). Bu ekvivalentlik yoxlanıldı:

- Mənbə tarixləri geriyə yazıla bilmir: `Sale.Create`/`CreateManual` `Date = DateTime.UtcNow` təyin edir, `ReviseCatalogued`/`ReviseManual` isə `Date`-ə toxunmur; ilkin borc yalnız müştəri yaradılarkən (`CreateCustomerHandler`) yazılır, `UpdateCustomerCommand` borca ümumiyyətlə toxunmur. Yəni sonradan "daha köhnə" mənbə peyda ola bilmir → ardıcıl (ödəniş-ödəniş) FIFO ilə cəm-FIFO eyni bölgünü verir.
- Mənbə silinməsi (`DELETE /credits/{saleId}` → `ReverseDebt`, sıfırda floor) halları əl ilə modelləşdirildi (ödənilmiş mənbənin silinməsi, ödənilməmiş mənbənin silinməsi, artıq ödəniş qalığı) — hər üç ssenaridə Σ remaining = `Customer.Debt` invariantı pozulmur; e2e probe-u (`DELETE` + yenidən oxuma) bunu faktiki olaraq təsdiqlədi.
- Tam ödənilmiş Nağd/Kart satış (`TotalAmount − PaidAmount = 0`) `GetOutstandingSalesAsync` filtrindən keçmir → borc mənbəyi sayılmır (QA probe: eyni müştəriyə həm qismən nisyə, həm tam nağd satış → yalnız nisyə qalığı sətri görünür, `originalAmount = 300`, satışın yekunu 500 deyil).

## İcra olunan test əmrləri

```bash
git -C backend diff origin/main...HEAD --stat     # 19 fayl, +854 / −3
dotnet build MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be21/   # 0 warning, 0 error
dotnet test  MayaPro.WarehouseApi.sln -p:BaseOutputPath=bin-be21/   # 479/479 yaşıl

# Əlavə QA probe-ları (müvəqqəti fayllar; icra edilib, nəticə alınıb, sonra SİLİNİB —
# repoya commit edilməyib, tətbiq kodu dəyişdirilməyib):
#   QaBe21TempTests.cs        (Customers.Tests)   → 8/8 Passed
#     TC4 daysOld=0, TC4 gələcək tarix floor, TC7 tam artıq ödəniş, TC7 qismən artıq ödəniş,
#     TC6 ilkin borc/satış eyni anlıq determinizm, TC5 kəsrli cəm, TC8 boş baza, AC4 warning
#   QaBe21TempApiTests.cs     (IntegrationTests)  → 7/7 Passed
#     AC8 anonim 401, AC8 Satıcı 200, TC8 borcsuz müştəri, qismən ödənişli satış originalAmount,
#     AC6 tam siyahı üzrə sıra invariantı, AC4 nisyə silindikdən sonra Σ=Debt, tam bağlanma
#   QaBe21TempApiScaleTests.cs (IntegrationTests) → 1/1 Passed
#     AC5: rows(small)=6 in 13 ms | rows(big)=86 in 17 ms (43 müştəri), hər müştəri üçün Σ=Debt
```

QA sessiyasından sonra işçi ağac təmizdir (`git status --short` → boş), yəni tətbiq və test kodu QA tərəfindən dəyişdirilməyib; bu report yeganə əlavədir.

## Yekun qərar

**QA PASSED.** AC1–AC8 və TC1–TC8 tam örtülüb və PASS-dır; funksional bug aşkar edilməyib, bug taskı yaradılmayıb. İki test örtüyü GAP-ı (GAP-1, GAP-2) və üç bloklamayan müşahidə (OBS-1…OBS-3) yalnız məlumat/backlog üçün qeyd olunub — release-i bloklamır.
