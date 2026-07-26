# ADR-0004: Satışda snapshot sahələri (ad, kateqoriya, maya)

**Status:** Qəbul edilib

## Qərar

Satış yazılarkən məhsulun adı, kateqoriyası, real mayası (`CostPerUnit`) və alış qiyməti (`PurchasePricePerUnit`) satır üzərinə **kopyalanır**. Məhsul sonradan redaktə olunsa və ya silinsə belə tarixi satış siyahısı və qazanc hesabatı dəyişmir.

Maya və alış qiyməti iki AYRI sahədir, biri digərindən çıxarılmır: maya partiya xərclərini də özündə saxlayır, alış qiyməti isə təmiz alışdır. Qazanc yalnız mayadan hesablanır. Ayrı sinif (ProductSnapshot) yaradılmır — sahələr birbaşa `Sale` entity-sindədir.

Eyni prinsip başqa yerlərdə: `Expense.ProductName` snapshot; `Product.Category` sərbəst mətn snapshot-dur (Category cədvəlinə FK deyil — kateqoriya adı dəyişəndə mövcud məhsullar pozulmur).

## Əlaqəli qayda

Maya bilinməyəndə (sərbəst satış, cost verilməyib) `Profit = null` — hesabatlar bunu 0 qazanc kimi yox, "naməlum" kimi sayır və ayrıca göstərir.

## Last Updated
2026-07-26 — snapshot sahələrinə `PurchasePricePerUnit` əlavə olundu

## Related Code
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs`
