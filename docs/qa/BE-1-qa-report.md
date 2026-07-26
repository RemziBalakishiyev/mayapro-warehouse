# QA Report — BE-1: Sale-də maya və xərcin ayrılması: PurchasePricePerUnit

**Tarix:** 2026-07-26
**QA Agent:** qa-tester
**Test edilən PR(lar):** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/2 (branch `task/BE-1-purchase-price-per-unit`, commit `fc7c089` HEAD)
**Mühit:** Lokal, Windows, .NET 8 SDK, SQL Server (localhost, `MayaProWarehouse` test DB-ləri) — `dotnet build` / `dotnet test` bütün solution üzərində

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi test case | 10 (TC-1..TC-10) |
| ✅ Pass | 10 |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Yaradılan bug sayı | 0 |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build` → Build succeeded, 0 Warning(s), 0 Error(s).
Test: `dotnet test` (bütün solution) → **183/183 keçdi**, 0 uğursuz, 0 skip (bax aşağıda modul-modul bölgü).

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC-1 | Migration: yeni sütun, snapshot/config yenilənməsi, `Down()` | ✅ | `20260726142954_AddSalePurchasePricePerUnit.cs`: `decimal(18,2)` NULL sütun əlavə olunur; `SalesDbContextModelSnapshot.cs`-də `PurchasePricePerUnit` mövcuddur; `Down()` sütunu `DropColumn` ilə geri silir. `SaleConfiguration.cs`-də ayrıca `Property` konfiqurasiyası yoxdur (EF konvensiya ilə `decimal?` sahəni artıq migration-da təyin olunmuş tip/dəqiqliklə uyğunlaşdırır) — bu, layihədəki digər nullable decimal sahələrlə (məs. `CostPerUnit`) eyni yanaşmadır, problem deyil. |
| AC-2 | Normal satış snapshot: `PurchasePricePerUnit` məhsulun cari `PurchasePrice`-i, `CostPerUnit` dəyişmir | ✅ | `ProductStockSnapshot` record-una `PurchasePrice` əlavə olunub (`IProductsModule.cs`); `ProductsModuleContract.TryDecreaseStockAsync` bunu `product.PurchasePrice`-dən doldurur; `CreateSaleHandler` → `Sale.Create(..., stock.Value.RealCostPerUnit, stock.Value.PurchasePrice, ...)` — `CostPerUnit` və `PurchasePricePerUnit` paralel, düzgün sıra ilə ötürülür. Test: `Create_Snapshots_PurchasePrice_Separately_From_RealCost` (SaleTests.cs), `Catalogued_Sale_Snapshots_Product_Purchase_Price_Beside_Its_Real_Cost` (SalesApiTests.cs, uçdan-uca). |
| AC-3 | Sərbəst satış: `PurchasePricePerUnit` command-dan olduğu kimi gəlir, `CostPerUnit`-i əvəz etmir | ✅ | `CreateSaleCommand.PurchasePricePerUnit` (nullable decimal) əlavə olunub; `CreateSaleHandler` bunu birbaşa `Sale.CreateManual(..., command.PurchasePricePerUnit)`-ə ötürür, heç bir yenidən-hesablama yoxdur. Test: `CreateManual_Separates_PurchasePrice_From_ComputedCost` (TC-1), `Manual_Sale_Round_Trips_Supplied_Purchase_Price` (uçdan-uca). |
| AC-4 | Mövcud Profit/CostPerUnit məntiqi toxunulmaz | ✅ | `Sale.cs`-də `Profit` düsturu `(UnitPrice − CostPerUnit) × Quantity` (və ya `null`) dəyişməyib; `PurchasePricePerUnit` ayrıca sahə kimi əlavə olunub, heç bir formula onu istifadə etmir. Bütün köhnə `SaleTests.cs` testləri (Create/CreateManual/ReviseCatalogued/ReviseManual üçün subtotal/total/profit yoxlamaları) dəyişməz qalıb və keçir. |
| AC-5 | DTO/wire: `purchasePricePerUnit` camelCase, nullable | ✅ | `SaleDto` və `SaleDetailDto`-da `PurchasePricePerUnit` sahəsi var; `SaleMapping.ToDto`/`ToDetailDto` bunu `sale.PurchasePricePerUnit`-dən doldurur. ASP.NET Core-un default camelCase JSON siyasəti ilə wire-da `purchasePricePerUnit` kimi görünür (`Manual_Sale_Round_Trips_Supplied_Purchase_Price`, `Catalogued_Sale_Snapshots_Product_Purchase_Price_Beside_Its_Real_Cost` testlərində JSON round-trip yoxlanıb). |
| AC-6 | Update axını: normal → yenidən snapshot, manual → command-dan | ✅ | `UpdateSaleHandler` → `ReviseCatalogued`-ə `stock.Value.PurchasePrice` ötürülür (yeni snapshot); `ReviseManual`-a `command.PurchasePricePerUnit` ötürülür. `ReviseManual` parametri **məcburi** edilib (default yoxdur) — bu, "sessiz silinmə" reqressiyasının qarşısını alır (senior-un düzəltdiyi problem). Test: `Update_Catalogued_Sale_Resnapshots_The_Products_Current_Purchase_Price` (TC-9, uçdan-uca), `ReviseCatalogued_Resnapshots_PurchasePrice`, `ReviseManual_Rewrites_PurchasePrice_And_Leaves_Cost_Independent`. |
| AC-7 | Migration backfill qaydaları | ✅ | Backfill SQL `IsManual = 1` şərti, `Quantity = 0` üçün sıfıra bölmənin qarşısı (`WHEN s.Quantity = 0 THEN s.CostPerUnit`), `CostPerUnit IS NULL` üçün NULL saxlanması, normal sətirlərə toxunmama (`WHERE s.IsManual = 1`) və idempotentlik (`AND s.PurchasePricePerUnit IS NULL`) doğru tətbiq olunub. `ISJSON`/`TRY_CAST` müdafiəsi korlanmış JSON-un bütün migrasiyanı yıxmasının qarşısını alır. Real SQL Server üzərində `SalesMigrationTests.cs` (7 hal: TC-4, TC-5, TC-6, TC-7 + sıfır say + korlanmış JSON + qeyri-bərabər bölünmə) ilə təsdiqlənib. |
| AC-8 | Null-safety/validasiya: manual-da `null` icazəli, mənfi rədd edilir | ✅ | `CreateSaleValidator`/`UpdateSaleValidator`-da `RuleFor(x => x.PurchasePricePerUnit).GreaterThanOrEqualTo(0m).When(x => x.PurchasePricePerUnit is not null)` — null keçir, mənfi rədd olunur (sıfır və müsbət qəbul olunur). Test: `Negative_PurchasePricePerUnit_Is_Invalid`, `Null_PurchasePricePerUnit_Is_Valid`, `Zero_PurchasePricePerUnit_Is_Valid` (unit), `Negative_Purchase_Price_Returns_400_And_Writes_No_Sale` (uçdan-uca, 400 + "heç nə yazılmayıb" yoxlanılıb). |

## Test case nəticələri

| # | Ssenari | Nəticə | Faktiki davranış / Qeyd |
|---|---|---|---|
| TC-1 | Sərbəst satış — tam nümunə (costPerUnit=125, purchasePricePerUnit=100, quantity=2, unitPrice=200) | ✅ | `CreateManual_Separates_PurchasePrice_From_ComputedCost` və `Manual_Sale_Round_Trips_Supplied_Purchase_Price` (uçdan-uca) — PurchasePricePerUnit=100, CostPerUnit=125, Profit=150, Subtotal=TotalAmount=400 dəqiq təsdiqlənib. |
| TC-2 | Normal satış — snapshot (PurchasePrice=80, RealCostPerUnit=95, quantity=3, unitPrice=150) | ✅ | `Create_Snapshots_PurchasePrice_Separately_From_RealCost` və `Catalogued_Sale_Snapshots_Product_Purchase_Price_Beside_Its_Real_Cost` (uçdan-uca, real DB) — PurchasePricePerUnit=80/8 (snapshot), CostPerUnit dəyişməz, Profit düzgün. |
| TC-3 | Sərbəst satış — maya naməlum (hər ikisi null) | ✅ | `CreateManual_Without_PurchasePrice_Is_Null_Safe` və `Manual_Sale_Without_Purchase_Price_Stores_Null` (uçdan-uca) — istisna atılmır, hər üçü (PurchasePricePerUnit/CostPerUnit/Profit) null. |
| TC-4 | Backfill — sərbəst, xərcli (CostPerUnit=110, Quantity=2, xərclər cəmi=20) | ✅ | `SalesMigrationTests.Migration_Backfills_...` real SQL Server üzərində: 110 − (20/2) = 100 — dəqiq təsdiqlənib. |
| TC-5 | Backfill — sərbəst, xərcsiz (CostPerUnit=75, Quantity=4, boş massiv) | ✅ | Eyni test: PurchasePricePerUnit=75 (çıxılma yoxdur). |
| TC-6 | Backfill — normal sətir (IsManual=0, CostPerUnit=95) | ✅ | Eyni test: PurchasePricePerUnit=NULL (toxunulmayıb). |
| TC-7 | Backfill — CostPerUnit naməlum (NULL, Quantity=1) | ✅ | Eyni test: PurchasePricePerUnit=NULL, migration xətasız tamamlanır. Əlavə olaraq sıfır say (0/0 bölmə qorunması) və korlanmış JSON (`"not json at all"`) halları da ayrıca yoxlanıb və keçib. |
| TC-8 | DTO/API cavabı — GET sales-by-id, purchasePricePerUnit=100 | ✅ | `ToDto_Carries_PurchasePricePerUnit` (unit) + `Manual_Sale_Round_Trips_Supplied_Purchase_Price` / `Catalogued_Sale_Snapshots_Product_Purchase_Price_Beside_Its_Real_Cost` (uçdan-uca, real HTTP JSON round-trip) — həm `SaleDto`, həm `SaleDetailDto`-da camelCase `purchasePricePerUnit` mövcuddur. |
| TC-9 | Update — normal satışın yenidən snapshotu (PurchasePrice 80→90) | ✅ | `ReviseCatalogued_Resnapshots_PurchasePrice` (unit) + `Update_Catalogued_Sale_Resnapshots_The_Products_Current_Purchase_Price` (uçdan-uca) — redaktə sonrası yeni snapshot (90/12) düzgün oturur; redaktə edilməyən köhnə sətirlərə toxunulmur (əlaqəli `ReviseCatalogued` yalnız çağırılan sətri dəyişir, digərləri toxunulmaz qalır — domain modelində başqa sətirlərə istinad yoxdur). |
| TC-10 | Mənfi purchasePricePerUnit — validasiya | ✅ | `Negative_PurchasePricePerUnit_Is_Invalid` (unit) + `Negative_Purchase_Price_Returns_400_And_Writes_No_Sale` (uçdan-uca) — 400 Bad Request, "Alış qiyməti mənfi ola bilməz" mesajı, sale sayının artmadığı təsdiqlənib (stock/debt dəyişməzliyi dolayı yolla `Result.Failure` handler-ə çatmadan validasiyada kəsilməsi ilə təmin olunur). |

## Xüsusi diqqət göstərilən nöqtələr (kod baxışı ilə təsdiqlənib)

- **Migration backfill SQL**: `ISJSON`/`TRY_CAST` müdafiəsi var, `Quantity = 0` sıfıra bölməni önləyir, `CostPerUnit IS NULL` halı ayrıca idarə olunur, normal sətirlər (`IsManual = 0`) toxunulmur, `AND s.PurchasePricePerUnit IS NULL` filtri statementi idempotent edir (təkrar işə salınsa mövcud dəyərləri əzmir).
- **Down() migration**: sadəcə `DropColumn` — kifayətdir, çünki backfill `Sql()` çağırışının geri qaytarılması mənasız olardı (sütun silinəndə data da gedir).
- **Mənfi dəyər validasiyası**: həm `CreateSaleValidator`, həm `UpdateSaleValidator`-da eyni qayda tətbiq olunub.
- **DTO camelCase wire formatı**: `SaleDto`/`SaleDetailDto` və `SaleMapping` düzgün doldurulur; ASP.NET Core-un standart camelCase JSON siyasəti ilə uyğundur.
- **Update axınında dəyərin itməməsi**: `Sale.ReviseManual`-da `purchasePricePerUnit` parametri məcburi edilib (əvvəlki versiyada default `null` səssizcə saxlanılan dəyəri silirdi — bu, senior review zamanı aşkarlanıb və düzəldilib, indi tələb olunan parametrdir).
- **Profit/costPerUnit reqressiyası**: `Sale.cs`-də bütün hesablama düsturları toxunulmayıb; köhnə `SaleTests.cs` testləri (Create/CreateManual/Revise*) heç bir dəyişiklik olmadan keçir.
- **Faktura PDF-i**: `ExportSaleInvoicePdfHandler.cs`/`PublicInvoicePdfHandler.cs` daxilində `PurchasePrice`/`PurchasePricePerUnit` istinadı yoxdur — alış qiyməti auth-suz açıq faktura linkinə sızmır (senior-un qeyd etdiyi kimi təsdiqlənib).

## İcra olunan test əmrləri

```bash
git -C ".../backend" checkout task/BE-1-purchase-price-per-unit
git -C ".../backend" pull origin task/BE-1-purchase-price-per-unit

dotnet build
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test
# MayaPro.WarehouseApi.SharedKernel.Tests            6/6 passed
# MayaPro.WarehouseApi.Modules.DayEnd.Tests          4/4 passed
# MayaPro.WarehouseApi.Modules.Reports.Tests         10/10 passed
# MayaPro.WarehouseApi.Modules.Customers.Tests       6/6 passed
# MayaPro.WarehouseApi.Modules.Sales.Tests           20/20 passed
# MayaPro.WarehouseApi.Modules.Products.Tests        24/24 passed
# MayaPro.WarehouseApi.Modules.Suppliers.Tests       4/4 passed
# MayaPro.WarehouseApi.Modules.Expenses.Tests        7/7 passed
# MayaPro.WarehouseApi.Modules.Auth.Tests            4/4 passed
# MayaPro.WarehouseApi.IntegrationTests              98/98 passed  (SalesApiTests, SalesMigrationTests,
#                                                                    CreateSaleValidatorTests daxil olmaqla)
# TOTAL: 183/183 passed, 0 failed, 0 skipped
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; PR#2 QA-nı problemsiz keçdi.
- `SaleConfiguration.cs`-də `PurchasePricePerUnit` üçün ayrıca `builder.Property(...)` yoxdur — EF konvensiyaya (migration-da təyin olunan `decimal(18,2)`) etibar olunur. Bu, mövcud `CostPerUnit` sahəsi ilə eyni yanaşma olduğu üçün risk kimi qiymətləndirilmir, amma gələcəkdə `SaleConfiguration.cs`-ə açıq `HasColumnType("decimal(18,2)")` əlavə olunması sənədləşmə baxımından faydalı ola bilər (bloklayıcı deyil).
- Backend taskı Done statusuna keçirilə bilər.
