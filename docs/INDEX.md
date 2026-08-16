# Documentation Index (Router)

Əvvəl bunu oxu, sonra YALNIZ task-a aid sənədləri aç. Kod həmişə son həqiqətdir — ziddiyyətdə kodu yoxla, sənədi düzəlt. Workflow: `.claude/rules/documentation.md`.

## Sənədlər

| Sənəd | Məzmun | Nə vaxt oxu | Əlaqəli kod |
|---|---|---|---|
| [business/BUSINESS-RULES.md](business/BUSINESS-RULES.md) | Bütün biznes qaydaları: rollar/icazələr, satış, stok/maya, borc, gün sonu | Hər hansı biznes məntiqi dəyişikliyində | `src/Modules/*/Domain/` |
| [business/GLOSSARY.md](business/GLOSSARY.md) | Domain terminləri (Nisyə, real maya, sərbəst satış...) və wire dəyərləri | Termin aydın olmayanda | `SharedKernel/Contracts/WireFormat.cs` |
| [flows/AUTH-FLOW.md](flows/AUTH-FLOW.md) | Login, JWT claims, policy-lər, demo istifadəçilər | Auth/icazə işlərində | `Modules.Auth/`, `Api/Extensions/` |
| [flows/SALE-FLOW.md](flows/SALE-FLOW.md) | Satış zənciri: create/update/delete, transaction, geri sarma | Satış və ya stok/borc zəncirinə toxunanda | `Modules.Sales/Application/UseCases/` |
| [flows/CUSTOMER-DEBT-FLOW.md](flows/CUSTOMER-DEBT-FLOW.md) | Borc artımı/azalması, ödəniş, tarixçə, nisyə silmə | Müştəri/borc işlərində | `Modules.Customers/` |
| [flows/EXPENSE-COST-FLOW.md](flows/EXPENSE-COST-FLOW.md) | Xərc→məhsul real maya zənciri | Xərc/maya işlərində | `Modules.Expenses/`, `Products/Domain/Product.cs` |
| [flows/DAYEND-FLOW.md](flows/DAYEND-FLOW.md) | Gün bağlama, ExpectedCash düsturu, bağlı gün qoruması | DayEnd/kassa işlərində | `Modules.DayEnd/` |
| [flows/EXPORT-FLOW.md](flows/EXPORT-FLOW.md) | Excel, dövr PDF, A5 qaimə-faktura | Export/PDF işlərində | `Modules.Exports/` |
| [api/API-OVERVIEW.md](api/API-OVERVIEW.md) | 40 endpoint-in tam cədvəli (verb, route, icazə) | Endpoint əlavə/dəyişəndə, icazə suallarında | `src/Modules/*/Endpoints/` |
| [api/ERROR-CONTRACT.md](api/ERROR-CONTRACT.md) | `{code, message}` formatı, suffix→status qaydası, validation | Yeni error/status işlərində | `SharedKernel/Application/ResultExtensions.cs` |
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | Stack, host pipeline, IModule, use-case üslubu, test strategiyası | Yeni modul/infrastruktur işlərində | `Api/`, `SharedKernel/` |
| [multi-tenancy.md](multi-tenancy.md) | Çox mağazalı (multi-tenant) SaaS: Mərhələ 1 — data təcridi (`ICurrentTenant`, query filter + interceptor, tenant-scoped unikallıq, təhlükəsizlik auditi); Mərhələ 2 — qeydiyyat axını, platforma admini (`TenantId` qərarı), abunə/`ExpiresAt` qaydası, avto-blok, `IgnoreQueryFilters` allowlist testi | Tenant/təcrid, yeni cədvəl, qeydiyyat/abunə/admin, anonim və ya background icra yolu işlərində | `SharedKernel/Domain/TenantEntity.cs`, `SharedKernel/Infrastructure/Tenant*`, `Modules.Tenancy/`, `Api/Middleware/TenantGateMiddleware.cs` |
| [architecture/MODULES.md](architecture/MODULES.md) | Modul məsuliyyətləri + kontrakt provider/consumer xəritəsi | Modullararası əlaqə qurarkən | `SharedKernel/Contracts/` |
| [database/DATABASE.md](database/DATABASE.md) | Schema-lar, konvensiyalar, indekslər, migration qaydası, seed | Migration/DB işlərində | `src/Modules/*/Infrastructure/` |
| [database/ENTITY-RELATIONS.md](database/ENTITY-RELATIONS.md) | Entity xəritəsi, modullararası Id referansları (FK-sız) | Entity/relation dəyişikliyində | `src/Modules/*/Domain/` |
| [decisions/](decisions/) | ADR-lər: 0001 modular monolith, 0002 Result, 0003 UnitOfWork, 0004 snapshot, 0005 Bakı timezone, 0006 wire format, 0007 endirim ləğvi | Arxitektura qərarını sorğulayanda / yenisini verəndə | — |
| [changes/CHANGELOG.md](changes/CHANGELOG.md) | Əhəmiyyətli dəyişikliklərin qısa jurnalı | Yaxın tarixçəni bilmək lazımda | `git log` |

## Legacy referans faylları (dəyişdirmə, sil-mə)

| Fayl | Nədir |
|---|---|
| `backend-arxitektura.md` | İLKİN dizayn planı (köhnə "Sederek" adları) — tarixi kontekst; faktiki vəziyyət yuxarıdakı sənədlərdədir |
| `index.ts` | Frontend TS tipləri = DTO/wire kontraktı referansı |
| `handlers.ts` | Frontend mock handler-ləri = biznes davranış referansı |
| `seed.ts` | Frontend seed datası = dev seeder referansı |

## Last Updated

2026-08-16 — `multi-tenancy.md` Mərhələ 2 ilə genişləndi (BE#36: qeydiyyat, platforma admini, abunə).
2026-08-16 — `multi-tenancy.md` əlavə olundu (BE#35, Mərhələ 1: data təcridi).
2026-07-25 — sistem qurulanda yaradıldı.
