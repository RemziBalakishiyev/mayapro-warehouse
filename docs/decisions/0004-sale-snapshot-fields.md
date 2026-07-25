# ADR-0004: Satışda snapshot sahələri (ad, kateqoriya, maya)

**Status:** Qəbul edilib

## Qərar

Satış yazılarkən məhsulun adı, kateqoriyası və real mayası (`CostPerUnit`) satır üzərinə **kopyalanır**. Məhsul sonradan redaktə olunsa və ya silinsə belə tarixi satış siyahısı və qazanc hesabatı dəyişmir.

Eyni prinsip başqa yerlərdə: `Expense.ProductName` snapshot; `Product.Category` sərbəst mətn snapshot-dur (Category cədvəlinə FK deyil — kateqoriya adı dəyişəndə mövcud məhsullar pozulmur).

## Əlaqəli qayda

Maya bilinməyəndə (sərbəst satış, cost verilməyib) `Profit = null` — hesabatlar bunu 0 qazanc kimi yox, "naməlum" kimi sayır və ayrıca göstərir.

## Last Updated
2026-07-25

## Related Code
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs`
