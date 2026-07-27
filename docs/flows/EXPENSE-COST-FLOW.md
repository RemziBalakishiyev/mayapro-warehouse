# Expense → Cost Flow — xərc-maya zənciri

## Xərc yaradılması (`POST /api/expenses`, Owner+Manager)

Tək transaction-da:

1. Validation: ad boş olmamalı, məbləğ > 0, kateqoriya wire dəyərlərindən biri olmalıdır (Yol, Fəhlə, Anbar/Yer, Paket/Qutu, Mağaza, Digər), `date` göndərilibsə Bakı günü ilə **gələcək ola bilməz** (`IDateProvider.ToLocalDate(date) <= Today`, ADR-0005) → 400. `date` yoxdursa `IDateProvider.UtcNow` yazılır.
2. Xərc məhsula bağlıdırsa (`productId` var):
   - `IProductsModule.GetSnapshotAsync` — məhsul adı snapshot + mövcudluq yoxlaması (yoxdursa rollback).
   - `AddExpenseToProductAsync(productId, kateqoriyaKodu, amount)` — məhsulun xərc sətirlərinə əlavə olunur (eyni adlı sətir varsa üstünə gəlir) → **RealCostPerUnit yenidən hesablanır**: `PurchasePrice + Σxərclər ÷ InitialQuantity`.
3. `Expense` yazılır (məhsul adı snapshot-u ilə), activity log, commit.

## Düzəliş / silinmə (Owner+Manager)

- **Update**: eyni validation (gələcək tarix qadağası daxil, bağlı gün yoxlamasından əvvəl işləyir) + reverse-and-reapply — köhnə məbləğ məhsuldan çıxarılır (`RemoveExpense`, tanınmayan sətir no-op), yeni məbləğ əlavə olunur; məhsul dəyişibsə köhnədən çıxıb yeniyə yazılır.
- **Delete**: məhsula bağlı idisə `RemoveExpense` ilə maya effekti geri sarılır, sonra xərc silinir.

## Məhsul tərəfi

`Product.Expenses` sərbəst adlı sətirlər (JSON sütun). Yaradılış/redaktə formasından da gəlir. Formula: bax BUSINESS-RULES → "Stok və maya".

## Gün sonu ilə əlaqə

`IExpensesModule.GetDayTotalAsync(date)` — Bakı gününün xərc cəmi `ExpectedCash` hesabına girir (bax DAYEND-FLOW).

## Last Updated

2026-07-27 — validation addımı dəqiqləşdi: xərc tarixi gələcək ola bilməz (BE#9).

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Application/UseCases/`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs` (AddExpense/RemoveExpense/CalculateRealCost)
