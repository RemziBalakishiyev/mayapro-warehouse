# ADR-0007: Endirim (Discount) sahəsinin ləğvi

**Status:** Qəbul edilib (2026-07-25, commit `4a9cf08`)

## Qərar

`Sale.Discount` sahəsi domain-dən, DTO-lardan, validatorlardan və DB-dən (migration `RemoveSaleDiscount`) tamamilə çıxarıldı. Artıq `TotalAmount = Subtotal = UnitPrice × Quantity` və `Profit = (UnitPrice − CostPerUnit) × Quantity`.

## Səbəb

Bazar praktikasında endirim ayrıca sahə kimi istifadə olunmurdu — satıcı sadəcə satış qiymətini aşağı yazır. Sadələşdirmə hesablama xətası risklərini azaldır.

## Nəticə

Faktura və hesabatlarda endirim sətri yoxdur. Yeni feature endirim tələb edərsə, bu ADR-ə yenidən baxılmalı və sahə şüurlu şəkildə geri qaytarılmalıdır (migration `Down` mövcuddur).

## Last Updated
2026-07-25

## Related Code
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Infrastructure/Migrations/20260723120000_RemoveSaleDiscount.cs`
