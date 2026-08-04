# DayEnd Flow — gün sonu bağlanışı

## Bağlama (`POST /api/closings`, yalnız Owner)

1. Validation: `openingCash ≥ 0`, `actualCash ≥ 0`.
2. Gün = `IDateProvider.Today` (Bakı günü).
3. Pre-check: həmin günə bağlanış varsa `DayEnd.AlreadyClosed` (409). Race qoruması: `Date` unique index — `DbUpdateException` də `AlreadyClosed`-a çevrilir.
4. Server totalları özü hesablayır: `ISalesModule.GetDayTotalsAsync(date)` → nağd/kart/nisyə; `IExpensesModule.GetDayTotalAsync(date)`; `ISalaryModule.GetDayPaymentsTotalAsync(date)`.
   BE#15-dən sonra ilk üç rəqəm REAL alınan pula əsaslanır (tək qruplaşdırılmış sorğu): **nağd** = `PaidVia = Cash` olan satışların `PaidAmount` cəmi (nisyə satışın nağd ilkin ödənişi də daxil), **kart** = `PaidVia = Card` üçün eyni, **nisyə** = nisyə satışların `TotalAmount − PaidAmount` (yalnız ödənilməmiş qalıq) cəmi.
   BE#28-dən sonra **işçi maaş ödənişləri** (`SalaryEntry.Type = Payment`, `Date` Bakı gününə düşən) də kassadan çıxan puldur və `Closing.Create(...)`-a ötürülməzdən əvvəl xərc cəminə əlavə olunur: `expenses = xərclər + maaş ödənişləri`. Maaş **tutulmaları** (`Deduction`) sorğuya heç düşmür: fiziki pul çıxmır, yalnız işçinin hesabından tutulur.
   BE#33-dən sonra bu maaş cəmi `Closing.SalaryExpenses` sütununda AYRICA da saxlanılır və `ClosingDto.salaryExpenses`-ə düşür — `Expenses`/`ExpectedCash` düsturları DƏYİŞMİR, yalnız artıq mövcud rəqəmin bir hissəsi ayrıca görünür (additiv sahə, ADR-0006-nı pozmur — dondurulmuş wire DƏYƏRLƏRİ, məs. ödəniş növləri, toxunulmayıb).
5. `Closing.Create(...)` — düsturlar constructor-da: `ExpectedCash = OpeningCash + CashSales − Expenses`, `Difference = ActualCash − ExpectedCash`. Nisyənin ödənilməmiş qalığı kassaya daxil deyil.
6. Activity log ("Gün sonu bağladı"), transaction commit.

## Oxuma

- `GET /api/closings` — hamısı, yeni gün birinci.
- `GET /api/closings/today` — bugünkü və ya null. ⚠️ Hazırda bu handler günü UTC ilə hesablayır (`DateOnly.FromDateTime(DateTime.UtcNow)`), CloseDay isə Bakı günü ilə — gecə pəncərəsində uyğunsuzluq mümkündür (düzəliş üçün task açılıb).

## Digər modullara təsiri

- **Sales**: günü bağlanmış satış REDAKTƏ oluna bilməz (`ClosingExistsAsync` qoruması UpdateSale-də; Delete-də qoruma yoxdur).
- **Reports**: dashboard `ExpectedCash` son bağlanışın `ActualCash`-inə lövbərlənir + ondan sonrakı REAL alınan nağd (`SalesReportRow.ReceivedVia = Nağd` sətirlərinin `ReceivedAmount` cəmi) − xərclər − maaş ödənişləri. Gün sonu ilə eyni düstur. Dashboard diapazon üzərində işlədiyi üçün maaş sətirlərini `ISalaryModule.GetPaymentsAsync(from, to)` ilə oxuyur (`IExpensesModule.GetExpensesAsync` ilə simmetrik); bağlanmış günün ödənişi ikinci dəfə çıxılmır. `todayExpenses` də bugünkü maaş ödənişlərini ehtiva edir. **BE#33:** `GetSummaryHandler` (`GET /api/reports/summary`, bağlanışdan ƏVVƏLKİ ön izləmə) eyni `ISalaryModule.GetPaymentsAsync(window.From, window.To)` ilə sorğulanan dövrün maaş ödənişlərini `SummaryDto.SalaryExpenses`-ə cəmləyir və `Expenses`/`NetProfit`-ə daxil edir (`expenses = generalExpenses + productExpenses + salaryExpenses`) — beləliklə bağlanışdan əvvəlki "Kassada olmalı məbləğ" ön izləməsi ilə faktiki bağlanışın rəqəmi heç vaxt fərqlənmir.
- **Auth (employees)**: `ISalaryModule` kontraktını təqdim edir. BE#28-də `AuthDbContext` paylaşılan `IDbConnectionFactory` bağlantısına keçdi və `ITransactionalDbContext` oldu — maaş sətri ilə activity log-u eyni transaction-da yazmaq üçün.

## Last Updated

2026-08-03 — BE#33: `SummaryDto.salaryExpenses` və `ClosingDto.salaryExpenses` əlavə olundu — bağlanışdan əvvəlki ön izləmə ilə faktiki bağlanış eyni maaş rəqəmini göstərir.

2026-08-01 — BE#28: gün cəminə işçi maaş ödənişləri əlavə olundu (`ISalaryModule.GetDayPaymentsTotalAsync`); tutulmalar kassaya toxunmur.

2026-07-30 — BE#15: gün totalları real alınan pula keçdi (nağd/kart = `PaidAmount`, nisyə = qalıq).

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.DayEnd/` (Domain/Closing.cs, UseCases/)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/UpdateSale/UpdateSaleHandler.cs` (bağlı gün qoruması)
