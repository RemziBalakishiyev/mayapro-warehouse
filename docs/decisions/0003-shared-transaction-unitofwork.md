# ADR-0003: Modullararası zəncirlər üçün paylaşılan transaction (UnitOfWork)

**Status:** Qəbul edilib

## Qərar

Scope-a bir paylaşılan SQL connection verilir (`IDbConnectionFactory`); `IUnitOfWork.BeginTransactionAsync` bir `DbTransaction` açır və bütün qeydiyyatlı `ITransactionalDbContext`-ləri ona enlist edir. Zəncirə toxunmayan context-lər zərərsiz enlist olur (0 sətir save edir).

## İstifadə pattern-i (satış zənciri nümunəsi)

```
tx aç → stok azalt (Products kontraktı) → nisyədirsə borc artır (Customers kontraktı)
→ satışı yaz → activity log → tx.SaveChangesAsync → tx.CommitAsync
```

Hər hansı addım `Failure` qaytararsa handler commit-dən əvvəl return edir → `DisposeAsync` avtomatik rollback edir. Kontrakt metodları (məs. `IncreaseDebtAsync`) dəyişikliyi **save etmir** — caller öz unit of work-ündə commit edir.

## Last Updated
2026-07-25

## Related Code
- `src/MayaPro.WarehouseApi.SharedKernel/Infrastructure/UnitOfWork.cs`, `SqlConnectionFactory.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/CreateSale/CreateSaleHandler.cs` (kanonik nümunə)
