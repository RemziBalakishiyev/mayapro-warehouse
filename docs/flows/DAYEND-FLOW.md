# DayEnd Flow — gün sonu bağlanışı

## Bağlama (`POST /api/closings`, yalnız Owner)

1. Validation: `openingCash ≥ 0`, `actualCash ≥ 0`.
2. Gün = `IDateProvider.Today` (Bakı günü).
3. Pre-check: həmin günə bağlanış varsa `DayEnd.AlreadyClosed` (409). Race qoruması: `Date` unique index — `DbUpdateException` də `AlreadyClosed`-a çevrilir.
4. Server totalları özü hesablayır: `ISalesModule.GetDayTotalsAsync(date)` → nağd/kart/nisyə; `IExpensesModule.GetDayTotalAsync(date)`.
   BE#15-dən sonra bu üç rəqəm REAL alınan pula əsaslanır (tək qruplaşdırılmış sorğu): **nağd** = `PaidVia = Cash` olan satışların `PaidAmount` cəmi (nisyə satışın nağd ilkin ödənişi də daxil), **kart** = `PaidVia = Card` üçün eyni, **nisyə** = nisyə satışların `TotalAmount − PaidAmount` (yalnız ödənilməmiş qalıq) cəmi.
5. `Closing.Create(...)` — düsturlar constructor-da: `ExpectedCash = OpeningCash + CashSales − Expenses`, `Difference = ActualCash − ExpectedCash`. Nisyənin ödənilməmiş qalığı kassaya daxil deyil.
6. Activity log ("Gün sonu bağladı"), transaction commit.

## Oxuma

- `GET /api/closings` — hamısı, yeni gün birinci.
- `GET /api/closings/today` — bugünkü və ya null. ⚠️ Hazırda bu handler günü UTC ilə hesablayır (`DateOnly.FromDateTime(DateTime.UtcNow)`), CloseDay isə Bakı günü ilə — gecə pəncərəsində uyğunsuzluq mümkündür (düzəliş üçün task açılıb).

## Digər modullara təsiri

- **Sales**: günü bağlanmış satış REDAKTƏ oluna bilməz (`ClosingExistsAsync` qoruması UpdateSale-də; Delete-də qoruma yoxdur).
- **Reports**: dashboard `ExpectedCash` son bağlanışın `ActualCash`-inə lövbərlənir + ondan sonrakı REAL alınan nağd (`SalesReportRow.ReceivedVia = Nağd` sətirlərinin `ReceivedAmount` cəmi) − xərclər. Gün sonu ilə eyni düstur.

## Last Updated

2026-07-30 — BE#15: gün totalları real alınan pula keçdi (nağd/kart = `PaidAmount`, nisyə = qalıq).

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.DayEnd/` (Domain/Closing.cs, UseCases/)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/UpdateSale/UpdateSaleHandler.cs` (bağlı gün qoruması)
