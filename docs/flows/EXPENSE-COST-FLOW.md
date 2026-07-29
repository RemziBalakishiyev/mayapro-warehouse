# Expense → Cost Flow — xərc-maya zənciri

## Xərc növləri (ExpenseType)

İdarə olunan pick-list, `Category` (Products) ilə eyni pattern: ayrı `ExpenseTypes` cədvəli, unique ad, `GET/POST /api/expense-types` (hər ikisi hər rola açıq, dublikat → 400 `Expenses.ExpenseTypeDuplicate`). Seed (yalnız Development, `ExpenseTypeSeeder`): Yol pulu, Fəhlə pulu, Yer/Anbar xərci, Paket/Qutu, Gömrük, Mağaza xərci, Digər.

`Expense.Category` bu adın sərbəst-string SNAPSHOT-udur (FK yoxdur) — `Product.Category`-nin eyni prinsipi: növ sonradan silinsə/adı dəyişsə köhnə xərclər pozulmur.

Ad qaydaları: boşluqlar kəsilir (trim), dublikat yoxlaması **case-insensitive**-dir (handler adları kiçildərək müqayisə edir — DB collation-undan asılı deyil, `"Yol pulu"` == `"yol pulu"` → 400), ad ≤ 100 simvol (`ExpenseTypes.Name`/`Expenses.Category` sütun ölçüsü; uzun ad DB-yə çatmadan 400 qaytarır).

## Xərc mənbəyi (Source)

`Expense.Source` — daxildə enum (`ExpenseSource`), wire-da string: `"general"` | `"product"`.

- `"product"`: `ProductId` MƏCBURİDİR — mala bağlı, aşağıdakı maya zənciri işə düşür.
- `"general"`: `ProductId` GÖNDƏRİLMƏMƏLİDİR — ümumi mağaza xərci ("satışdan əlavə xərc"), heç bir malın real mayasına toxunmur.
- Validasiya hər iki istiqamətdə (`CreateExpenseValidator`/`UpdateExpenseValidator`): uyğunsuzluq `General.Validation` (400).

## Xərc yaradılması (`POST /api/expenses`, Owner+Manager)

Tək transaction-da:

1. Validation: ad boş deyil, məbləğ > 0, kateqoriya boş deyil, `source` `general`/`product`-dan biridir və `productId` ilə uyğundur; `date` göndərilibsə Bakı günü ilə **gələcək ola bilməz** (`IDateProvider.ToLocalDate(date) <= Today`, ADR-0005) → 400. `date` yoxdursa `IDateProvider.UtcNow` yazılır.
2. `source = "product"` olduqda:
   - `IProductsModule.GetSnapshotAsync` — məhsul adı snapshot + mövcudluq yoxlaması (yoxdursa rollback).
   - `AddExpenseToProductAsync(productId, category, amount)` — məhsulun xərc sətirlərinə əlavə olunur (eyni adlı sətir varsa üstünə gəlir, `category` sərbəst xərc növü adı kimi keçir) → **RealCostPerUnit yenidən hesablanır**: `PurchasePrice + Σxərclər ÷ InitialQuantity`.
   - `source = "general"` olduqda bu addım TAMAMİLƏ ATLANIR — heç bir `IProductsModule` çağırışı yoxdur.
3. `Expense` yazılır (məhsul adı snapshot-u və `Source` ilə), activity log, commit.

## Düzəliş / silinmə (Owner+Manager)

- **Update**: eyni validation (gələcək tarix qadağası daxil — bağlı gün yoxlamasından əvvəl işləyir) + reverse-and-reapply — köhnə `Source == product` idisə köhnə məbləğ məhsuldan çıxarılır (`RemoveExpense`, tanınmayan sətir no-op), yeni `source == product`-dursa yeni məbləğ əlavə olunur; məhsul dəyişibsə köhnədən çıxıb yeniyə yazılır. `general` ↔ `product` arası keçid də eyni reverse-and-reapply ilə düzgün işləyir.
- **Delete**: `Source == product` idisə `RemoveExpense` ilə maya effekti geri sarılır, sonra xərc silinir; `general` xərcin silinməsi heç bir mala toxunmur.

## Siyahı və filtrlər (`GET /api/expenses`)

`month` (`yyyy-MM`) ilə yanaşı optional `source=general|product` filtri — hər ikisi birgə tətbiq oluna bilər. Naməlum `source` dəyəri 400 (`Expenses.InvalidSource`) qaytarır, 500 atmır.

## Məhsul tərəfi

`Product.Expenses` sərbəst adlı sətirlər (JSON sütun). Yaradılış/redaktə formasından da gəlir. Formula: bax BUSINESS-RULES → "Stok və maya".

## Gün sonu və hesabat əlaqəsi

- `IExpensesModule.GetDayTotalAsync(date)` — Bakı gününün xərc cəmi (source-dan asılı olmayaraq) `ExpectedCash` hesabına girir (bax DAYEND-FLOW).
- `IExpensesModule.GetExpensesAsync` qaytardığı `ExpenseReportRow`-a `Source` sahəsi əlavə olundu — `GetSummaryHandler` bunu `generalExpenses`/`productExpenses` bölgüsünə çevirir (cəmi ümumi `Expenses`-ə bərabərdir, `NetProfit` düsturu dəyişməyib).

## Migration qeydi (`ExpenseTypesAndSource`)

- Köhnə `ExpenseCategory` enum-u (Transport/Labor/Storage/Packaging/Store/Other) ləğv edildi — `Category` indi sərbəst string (`nvarchar(100)`, əvvəl `nvarchar(20)`). Mövcud sətirlər Azərbaycanca adlara çevrildi: Transport→Yol pulu, Labor→Fəhlə pulu, Storage→Yer/Anbar xərci, Packaging→Paket/Qutu, Store→Mağaza xərci, Other→Digər.
- Yeni `Source` sütunu əvvəlcə nullable əlavə olunur, hər iki budaq açıq şəkildə backfill edilir (`ProductId` dolu → `product`, boş → `general`), sonra NOT NULL-a çevrilir. SQL default constraint-i QƏSDƏN istifadə olunmur: o, EF modelində olmayan bir constraint kimi cədvəldə qalar və gələcək migration-larda schema drift yaradardı. Backfill UPDATE-ləri təkrar icraya davamlıdır (idempotent).
- `Down()` geri qaytarır, lakin təbiətcə itkilidir: Azərbaycanca adlar köhnə enum adlarına çevrilir, bu migration-dan sonra yaradılmış (idarə olunan növ) adlar isə `Other`-ə yığılır ki, sütun `nvarchar(20)`-ə kiçilərkən truncation xətası verməsin və köhnə enum-əsaslı kod hər sətri oxuya bilsin.
- Testi: `tests/MayaPro.WarehouseApi.IntegrationTests/ExpensesMigrationTests.cs`.

## Last Updated

2026-07-27 — BE#4: idarə olunan xərc növləri (ExpenseType) + xərc mənbəyi ayrımı (Source), migration qeydi; review-dan sonra: ad qaydaları (case-insensitive dublikat, ≤100 simvol) və migration backfill/Down detalları. BE#9: validation addımı dəqiqləşdi — xərc tarixi gələcək ola bilməz.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Application/UseCases/`
- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Domain/ExpenseType.cs`, `ExpenseSource.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Infrastructure/Migrations/20260727120000_ExpenseTypesAndSource.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs` (AddExpense/RemoveExpense/CalculateRealCost)
- `src/Modules/MayaPro.WarehouseApi.Modules.Reports/Application/UseCases/GetSummary/GetSummaryHandler.cs`
