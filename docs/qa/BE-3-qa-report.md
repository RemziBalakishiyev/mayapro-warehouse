# QA Report — BE-3: Təchizatçı yaradılışına ilkin borc (InitialDebt)

**Tarix:** 2026-07-27
**QA Agent:** qa-tester
**Test edilən:** Issue https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/3, branch `task/BE-3-supplier-initial-debt`, commit `b4efcc2` (HEAD)
**Mühit:** Lokal, Windows, .NET 8 SDK (dotnet SDK 9.0.306 host), SQL Server (localhost, `MayaProWarehouse_Test` inteqrasiya test DB-si) — `dotnet build` / `dotnet test` bütün solution üzərində. API-nin standart `bin/Debug` çıxışı Visual Studio debug sessiyası tərəfindən kilidli olduğundan `-p:UseArtifactsOutput=true -p:ArtifactsPath=C:/temp/mayaqa` ilə alternativ çıxış qovluğundan build/test icra olundu.

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 5 (AC1–AC5) |
| Ümumi test case | 10 (TC1–TC10) |
| ✅ Pass | 15/15 (5 AC + 10 TC) |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Yaradılan bug sayı | 0 |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/temp/mayaqa` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test: `dotnet test -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/temp/mayaqa` (bütün solution) → **195/195 keçdi**, 0 uğursuz, 0 skip. Senior-backend-in iddia etdiyi "195/195 yaşıl" nəticəsi müstəqil təkrarlanaraq təsdiqləndi.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC1 | Debt = 0 (və ya göndərilmir): köhnə davranış saxlanılır | ✅ PASS | `CreateSupplierHandler` — `command.Debt > 0` şərti yalnış olduqda `SupplierDebtAdjustment` yaradılmır, activity log sadə `"{name}"` mesajı ilə yazılır (`"ilkin borc"` yoxdur). Kod: `CreateSupplierHandler.cs:47-50`. Unit: `Debt_Zero_Creates_Supplier_Without_Adjustment_Or_Debt_Wording` (TC1). İnteqrasiya: `Supplier_Created_Without_Debt_Has_Empty_History` (TC7) — həm `Debt==0`, həm `history` boş massiv real HTTP+DB üzərində təsdiqlənib. |
| AC2 | Debt > 0: borc, tarixçə qeydi və activity log bir transaction-da yaranır | ✅ PASS | `CreateSupplierHandler.cs:31-53` — `unitOfWork.BeginTransactionAsync` ilə açılan tək tx daxilində supplier + `SupplierDebtAdjustment.Create(supplier.Id, command.Debt, SupplierDebtAdjustment.InitialDebtNote, currentUser.UserId)` + activity log (`"Təchizatçı əlavə etdi"` / `"{name} — ilkin borc {amount:0.00} AZN"`) eyni `tx.SaveChangesAsync`/`CommitAsync` cütü ilə commit olunur — `CreateCustomerHandler`-dəki nümunə ilə eyni forma. Unit: `Debt_Positive_Creates_Supplier_With_Adjustment_And_Debt_Wording` (TC2) — `Amount=150`, `Note="İlkin borc (sistemə keçid)"`, `CreatedByUserId` doğru təsdiqlənib. |
| AC3 | Tarixçə endpoint-i ilkin borc sətrini qaytarır | ✅ PASS | `GetSupplierHistoryHandler` `SupplierDebtAdjustment` qeydlərini `Type="initialDebt"` kimi map edir. İnteqrasiya: `Supplier_Created_With_Initial_Debt_Sets_Debt_And_Records_History_Row` (TC6) — dəqiq 1 element, `Type="initialDebt"`, `Amount=150`, `Note="İlkin borc (sistemə keçid)"` real DB üzərində doğrulanıb. |
| AC4 | Tarixçə xronoloji sıralanır və köhnə /payments endpoint-i pozulmur | ✅ PASS | `GetSupplierHistoryHandler.cs:36` — `entries.OrderBy(e => e.Date)`; köhnə `GetSupplierPaymentsHandler` toxunulmayıb. İnteqrasiya: `History_Returns_Initial_Debt_And_Payment_In_Chronological_Order_And_Payments_Endpoint_Stays_Unchanged` (TC8) — 2 element (`initialDebt 150` → `payment 50`), eyni ssenaridə `/payments` yalnız 1 element (`Amount=50`) qaytarır. |
| AC5 | Mənfi Debt: validasiya dəyişmir, heç nə yaranmır | ✅ PASS | `CreateSupplierValidator.cs:12-13` — `RuleFor(x => x.Debt).GreaterThanOrEqualTo(0).WithMessage("Borc mənfi ola bilməz")` dəyişməyib. Unit: `Negative_Debt_Fails_Validation_And_Persists_Nothing` (TC3) — `Suppliers` və `SupplierDebtAdjustments` sayı 0, `activityLogger.Entries` boş. İnteqrasiya: `Negative_Initial_Debt_Returns_400_And_Creates_Nothing` (TC9) — 400, mesaj `"Borc mənfi ola bilməz"`, supplier siyahıda yoxdur. |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Qeyd |
|---|---|---|---|
| TC1 | CreateSupplierHandler, Debt=0 | ✅ PASS | `Debt_Zero_Creates_Supplier_Without_Adjustment_Or_Debt_Wording` — `Result.Success`, `SupplierDebtAdjustments` count 0, activity mesajında "ilkin borc" yoxdur. |
| TC2 | CreateSupplierHandler, Debt=150 (happy path) | ✅ PASS | `Debt_Positive_Creates_Supplier_With_Adjustment_And_Debt_Wording` — `supplier.Debt==150`, 1 adjustment (`Amount=150`, `Note=InitialDebtNote`), activity mesajında `"ilkin borc 150.00 AZN"` var. |
| TC3 | CreateSupplierHandler, Debt=-5 (error case) | ✅ PASS | `Negative_Debt_Fails_Validation_And_Persists_Nothing` — `Result.Failure`, mesaj `"Borc mənfi ola bilməz"`, `Suppliers`/`SupplierDebtAdjustments` 0, activity log boş. |
| TC4 | SupplierDebtAdjustment.Create (domain) | ✅ PASS | `Create_Sets_All_Fields_And_Stamps_The_Current_UTC_Time` — `SupplierId`, `Amount=150`, `Note="İlkin borc (sistemə keçid)"`, `CreatedByUserId`, `Date` UTC `[before, after]` aralığında. |
| TC5 | GetSupplierHistoryHandler, qarışıq tarixçə (edge case) | ✅ PASS | `Merges_Adjustment_And_Payments_In_Chronological_Order` — 1 adjustment (150) + 2 payment (50, 30, fərqli tarixlərdə) → 3 element, `Date`-ə görə artan, birinci `Type=InitialDebt`. |
| TC6 | POST /api/suppliers + ilkin borc (happy path, AC2+AC3) | ✅ PASS | `Supplier_Created_With_Initial_Debt_Sets_Debt_And_Records_History_Row` — real HTTP+SQL Server üzərində: `Debt==150`, `history` 1 element (`initialDebt`, 150, `"İlkin borc (sistemə keçid)"`). |
| TC7 | POST /api/suppliers borcsuz (edge case, AC1) | ✅ PASS | `Supplier_Created_Without_Debt_Has_Empty_History` — `Debt==0`, `history` boş massiv (`Assert.Empty`). |
| TC8 | İlkin borc + sonrakı ödəniş (AC4) | ✅ PASS | `History_Returns_Initial_Debt_And_Payment_In_Chronological_Order_And_Payments_Endpoint_Stays_Unchanged` — `/history` 2 element (`initialDebt 150`, `payment 50`), `/payments` yalnız 1 element (`Amount=50`). |
| TC9 | Mənfi ilkin borc (error case, AC5) | ✅ PASS | `Negative_Initial_Debt_Returns_400_And_Creates_Nothing` — 400, `"Borc mənfi ola bilməz"`, supplier `/api/suppliers` siyahısında yoxdur. |
| TC10 | Mövcud AddSupplierDebt/AddSupplierPayment axını (regression) | ✅ PASS | `Adding_Debt_Increases_Supplier_Debt`, `Payment_Reduces_Supplier_Debt`, `Delete_Supplier_With_Debt_Returns_409_And_Keeps_The_Supplier` və digər mövcud `SuppliersApiTests.cs` testləri (7 köhnə test) — hamısı dəyişikliksiz yaşıl qalır. |

## Müstəqil yoxlamalar (kod baxışı + build/test icrası ilə təsdiqlənib)

- **Build/test iddiasının təkrarlanması**: senior-backend-in "0 error/0 warning, 195/195 yaşıl" iddiası müstəqil təkrar icra ilə təsdiqləndi (aşağıda tam bölgü). VS debug kilidinə görə alternativ `ArtifactsPath` istifadə olundu, tapşırıqda göstərildiyi kimi.
- **Migration ↔ snapshot uyğunluğu**: `20260727120000_AddSupplierDebtAdjustments.cs` (`Up`/`Down` cüt) və `SuppliersDbContextModelSnapshot.cs`-də `SupplierDebtAdjustment` entity-si (sətir 64, `ToTable("SupplierDebtAdjustments","suppliers")`, sətir 97) uyğundur. İnteqrasiya testləri `WarehouseApiFactory.EnsureDatabaseResetAsync()` → `db.Database.MigrateAsync()` vasitəsilə **real lokal SQL Server-də** migrasiyanı faktiki tətbiq edir (`Server=localhost;Database=MayaProWarehouse_Test`) — 102/102 inteqrasiya testi bu real migrasiya üzərində keçib, o cümlədən TC6–TC9. Bu, migrasiyanın həqiqətən işlədiyinin ən güclü sübutudur (sadəcə kod baxışı deyil).
- **Regressiya**: bütün mövcud supplier testləri (`Adding_Debt_Increases_Supplier_Debt`, `Payment_Reduces_Supplier_Debt`, `Payment_Exceeding_Debt_Returns_400...`, `Update_Supplier_Changes_Details_And_Leaves_Debt_Untouched`, `Delete_Supplier_With_Debt_Returns_409...`, `Delete_Debt_Free_Supplier_Removes_The_Supplier`, `Seller_Cannot_Delete_Supplier_Returns_403`, `Supplier_ItemCount_Reflects_Linked_Products`) və bütün customer testləri dəyişikliksiz yaşıl qalır. `DeleteSupplierHandler` indi `SupplierDebtAdjustments`-i də təmizləyir (`DeleteSupplierHandler.cs:37-40`) — `Deleting_A_Settled_Supplier_Also_Removes_Their_Opening_Balance_History` unit testi ilə örtülüb, orphan adjustment qeydi qalmır.
- **Mənfi/sıfır/böyük dəyər halları**: mənfi (`-5`, `-10`) validator tərəfindən rədd edilir (TC3, TC9); sıfır (`Debt=0`) `command.Debt > 0` şərtinə görə heç bir adjustment yaratmır (TC1, TC7) — həm domain, həm HTTP səviyyəsində düzgün. `Debt` üçün üst hədd (max value) yoxdur, `decimal(18,2)` sütun tipi ilə məhdudlaşır — bu, Customer tərəfindəki eyni `InitialDebt` sahəsi ilə bərabər davranışdır, BE#3-ün əhatəsində yeni risk yaratmır.
- **Eyni anda payment + initial debt**: TC8 ssenarisi məhz bunu yoxlayır (`Debt=150` yaradılış + sonra `50` ödəniş) — `/history` düzgün 2 sətir, `/payments` isə köhnə kontraktı saxlayaraq yalnız ödənişi qaytarır. Uğurla keçir.
- **Bilinən boşluq — `POST /api/suppliers/{id}/debts` (`AddSupplierDebtHandler`) tarixçə sətri yaratmır**: kodu oxudum (`AddSupplierDebtHandler.cs`) — həqiqətən `SupplierDebtAdjustment` və ya digər auditable qeyd yazmır, sadəcə `supplier.IncreaseDebt(amount)` + activity log. Nəticədə `GET /history` cəmi bu yolla artırılan borcu əks etdirmir və `Supplier.Debt` ilə `history` cəmi arasında uyğunsuzluq yarana bilər. **Lakin bu, BE#3-ün əhatəsindən kənardır** — AC sənədində (`BE3-ac.md`, sətir 35-36) açıq şəkildə qeyd olunub: *"`AddSupplierDebt` (kredit alış) `Supplier.Debt`-i birbaşa artırır, ayrıca sorğulana bilən qeyd yaratmır — bu task-ın əhatəsindən kənardır."* AC1–AC5 və TC1–TC10-un heç biri bu davranışı əhatə etmir, Customer tərəfində də analoji "kredit artırma" axını yoxdur (müştəri borcu yalnız satışlardan artır, ayrıca handler yoxdur). Buna görə bug kimi qeyd edilmədi — gələcək task üçün texniki qeyd olaraq buraxılır, BE#3-ü blok etmir.
- **Canlı HTTP smoke test**: sandbox şəbəkə icazə siyasətinə görə `curl http://localhost:5208/...` birbaşa işə salınmadı (approval tələb olundu, sessiyada interaktiv təsdiq mümkün olmadı). Bunun əvəzinə inteqrasiya testləri (`WarehouseApiFactory` → real ASP.NET Core `WebApplicationFactory` + real SQL Server) artıq tam HTTP səviyyəli end-to-end örtük təmin edir (102/102 keçib) — bu, funksional olaraq eyni səviyyəli sübutdur.

## İcra olunan test əmrləri

```bash
git -C ".../backend" status
# On branch task/BE-3-supplier-initial-debt, up to date with origin, clean

git -C ".../backend" log --oneline -3
# b4efcc2 docs(BE#3) ...
# f7acb9b refactor(BE#3) ...
# 584d7df feat: techizatci ilkin borc

dotnet build -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/temp/mayaqa
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/temp/mayaqa
# MayaPro.WarehouseApi.SharedKernel.Tests            6/6 passed
# MayaPro.WarehouseApi.Modules.DayEnd.Tests          4/4 passed
# MayaPro.WarehouseApi.Modules.Sales.Tests           20/20 passed
# MayaPro.WarehouseApi.Modules.Reports.Tests         10/10 passed
# MayaPro.WarehouseApi.Modules.Expenses.Tests        7/7 passed
# MayaPro.WarehouseApi.Modules.Customers.Tests       6/6 passed
# MayaPro.WarehouseApi.Modules.Products.Tests        24/24 passed
# MayaPro.WarehouseApi.Modules.Suppliers.Tests       12/12 passed
# MayaPro.WarehouseApi.Modules.Auth.Tests            4/4 passed
# MayaPro.WarehouseApi.IntegrationTests              102/102 passed (SuppliersApiTests TC6-TC10 daxil, real SQL Server üzərində)
# TOTAL: 195/195 passed, 0 failed, 0 skipped
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; branch `task/BE-3-supplier-initial-debt` QA-nı problemsiz keçdi.
- `AddSupplierDebt` (kredit alış) axınının tarixçə qeydi yaratmaması — AC-dən kənar, lakin gələcəkdə supplier tarixçəsinin tam (100%) uyğunluğu üçün ayrı bir yaxşılaşdırma task-ı kimi PM-ə təklif oluna bilər (blocker deyil).
- Backend taskı **Done** statusuna keçirilə bilər.
