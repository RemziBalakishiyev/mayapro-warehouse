# Architecture

**Stack:** .NET 8, ASP.NET Core Minimal API, EF Core 8, SQL Server, JWT (HS256), FluentValidation, Serilog, ClosedXML (Excel), QuestPDF (PDF), BCrypt.

**Üslub:** Modular Monolith — tək host (`MayaPro.WarehouseApi.Api`), 11 izolyasiya olunmuş modul, schema-per-module (bax [ADR-0001](../decisions/0001-modular-monolith-schema-per-module.md)).

## Host pipeline (Program.cs)

`UseExceptionHandler` → `UseSerilogRequestLogging` → (dev-də Swagger) → `UseCors("Frontend")` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `/health` → modul endpoint-ləri → startup-da modul migration-ları (3 cəhd, artan gecikmə).

Rate limiting: host-da `PublicInvoice` policy (IP başına 30/dəq, fixed window, 429) — auth-suz açıq faktura endpoint-i üçün; modullar policy adını lokal const kimi təkrar bəyan edir (OwnerOnly pattern-i ilə eyni decoupling).

## Modul mexanizmi

- Hər modul `IModule` implement edir: `Name`, `RegisterServices`, `MapEndpoints`, `MigrateAsync`.
- Discovery: host yanındakı `MayaPro.WarehouseApi.*.dll`-lər force-load olunur, `IModule` implementasiyaları reflection ilə tapılıb ada görə sıralanır (`Api/Extensions/ModuleExtensions.cs`).
- Dev mühitində seeder-lər: Auth (demo istifadəçilər), Products, Customers, Suppliers. Sales/Expenses boş başlayır.

## Use case üslubu

MediatR YOXDUR. Hər use case bir qovluq: `Command` (record) + `Handler` (DI ilə endpoint-ə inject) + `Validator` (FluentValidation). Endpoint → handler → `Result` → `ToHttpResult()`.

## Kəsişən qayğılar

- **Xətalar:** Result pattern, Azərbaycanca mesajlar — `docs/api/ERROR-CONTRACT.md`
- **Transaction:** paylaşılan connection + `IUnitOfWork` — [ADR-0003](../decisions/0003-shared-transaction-unitofwork.md)
- **Saat:** UTC saxlama, Asia/Baku biznes günü (`IDateProvider`) — [ADR-0005](../decisions/0005-business-timezone-baku.md)
- **Audit:** `Entity` bazasında `CreatedAt`/`UpdatedAt`, `AuditInterceptor` avtomatik doldurur
- **Activity log:** yazan hər handler `IActivityLogger.LogAsync` çağırır (save etmir — caller-in transaction-ında commit olur)
- **Logging:** Serilog console + `logs/warehouse-.log` (gündəlik, 14 fayl)

## Konfiqurasiya açarları

`ConnectionStrings:Default` (tək DB), `App:TimeZone` (Asia/Baku), `App:PublicBaseUrl` (açıq faktura linklərinin bazası; dev `http://localhost:5208`), `Jwt:*` (Issuer/Audience/Secret ≥32 simvol/ExpiryHours=24), `Cors:FrontendOrigin` (default `http://localhost:5173`).

## Test strategiyası

- **Unit:** hər modulun öz test layihəsi (domain davranışı, kalkulyatorlar).
- **Integration:** `WarehouseApiFactory` real hostu `MayaProWarehouse_Test` lokal SQL Server DB-sinə qarşı qaldırır (Testcontainers YOXDUR); DB run-da bir dəfə drop+migrate+seed olunur. Login helper-ləri: `IntegrationTestHelpers` (owner `0501112233`, seller `0553334455`, şifrə `demo123`).
- Qayda: hər mərhələdən sonra `dotnet build` xətasız + testlər yaşıl → commit.

## Tarixi qeyd

`docs/backend-arxitektura.md` layihənin İLKİN dizayn planıdır (köhnə "Sederek" adları ilə) — faktiki vəziyyət bu sənədlər + koddur. Plan ilə fərqlər: MediatR yox (planda da yox idi), RowVersion concurrency YOXDUR (planda var idi), rol adları `Owner/Manager/Seller` (DB-də string), Testcontainers əvəzinə lokal SQL Server.

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/MayaPro.WarehouseApi.Api/` (Program.cs, Extensions/, Middleware/, Security/)
- `src/MayaPro.WarehouseApi.SharedKernel/`
- `tests/MayaPro.WarehouseApi.IntegrationTests/WarehouseApiFactory.cs`
