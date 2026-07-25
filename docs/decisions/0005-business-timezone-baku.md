# ADR-0005: Biznes saat qurşağı — Asia/Baku, saxlama UTC

**Status:** Qəbul edilib

## Qərar

Bütün timestamp-lər DB-də **UTC** saxlanır. "Bu gün" anlayışı ilə işləyən hər şey (gün sonu, gün totalları, dashboard, tarix filtrləri) `IDateProvider` üzərindən **Asia/Baku** (konfiq: `App:TimeZone`) saat qurşağına çevrilir.

Səbəb: Bakıda 00:30-da edilən satış (20:30 UTC) Bakı gününə düşməlidir, UTC gününə yox.

## Qayda

Handler-lərdə heç vaxt `DateTime.Now`/`Today` birbaşa istifadə etmə — `IDateProvider.Today`, `ToLocalDate`, `ToLocalDateTime`, `LocalDayRangeUtc` istifadə et. Gün filtri yarımaçıq UTC pəncərəsidir: `[StartUtc, EndUtc)`.

## Last Updated
2026-07-25

## Related Code
- `src/MayaPro.WarehouseApi.SharedKernel/Application/IDateProvider.cs`
- `src/MayaPro.WarehouseApi.SharedKernel/Infrastructure/AppDateProvider.cs`
