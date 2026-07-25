# ADR-0001: Modular Monolith — schema-per-module, tək database

**Status:** Qəbul edilib (layihənin təməli)

## Qərar

Tək deploy olunan ASP.NET Core proqramı, daxildə izolyasiya olunmuş modullar. Hər modulun öz DbContext-i, öz SQL schema-sı (`sales.Sales`, `products.Products`, ...) və öz migration tarixçəsi var.

## Qaydalar

- Modul başqa modulun cədvəlinə SQL səviyyəsində toxunmur: JOIN yox, FK yox. Modullararası referans sadəcə Guid saxlamaqdır (`Sale.CustomerId` navigation deyil).
- Modullararası çağırış YALNIZ `SharedKernel.Contracts`-dakı interfeyslərlə (in-process, DI).
- Hər modul `IModule` implement edir: `RegisterServices` + `MapEndpoints` + `MigrateAsync` (startup-da öz migration-ını tətbiq edir).
- Reports və Exports modullarının öz cədvəli yoxdur — yalnız kontraktlardan oxuyurlar.

## Nəticə

Tək database olduğu üçün modullararası zəncirlər bir transaction-da işləyir (bax ADR-0003) — mikroservisin distributed transaction problemi yoxdur; sabah bir modulu çıxarmaq minimal ağrı ilə mümkündür.

## Last Updated
2026-07-25

## Related Code
- `src/MayaPro.WarehouseApi.SharedKernel/Infrastructure/IModule.cs`
- `src/MayaPro.WarehouseApi.Api/Extensions/`
