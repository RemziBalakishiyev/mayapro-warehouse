# Database

Tək SQL Server DB (`MayaProWarehouse`), connection string: `ConnectionStrings:Default`. Test DB: `MayaProWarehouse_Test` (integration testlər lokal SQL Server-də).

## Schema-per-module

| Schema | DbContext | Cədvəllər | Transactional* |
|---|---|---|---|
| `identity` | AuthDbContext | Users | ❌ (öz connection-u) |
| `products` | ProductsDbContext | Products, Categories | ✅ |
| `sales` | SalesDbContext | Sales | ✅ |
| `customers` | CustomersDbContext | Customers, CustomerPayments, CustomerDebtAdjustments | ✅ |
| `suppliers` | SuppliersDbContext | Suppliers, SupplierPayments | ✅ |
| `expenses` | ExpensesDbContext | Expenses | ✅ |
| `dayend` | DayEndDbContext | Closings | ✅ |
| `activity` | ActivityDbContext | ActivityLogs | ✅ |
| `settings` | SettingsDbContext | StoreSettings (singleton sətir, sabit Id) | ❌ (standalone) |

\* Transactional = `ITransactionalDbContext` implement edir → paylaşılan UnitOfWork transaction-ına enlist olur. Reports/Exports modullarının cədvəli yoxdur.

## Konvensiyalar

- Bütün decimal-lar `decimal(18,2)` (context-lərdə global convention).
- `Entity` bazası: `Id` (Guid), `CreatedAt`, `UpdatedAt` — `AuditInterceptor` avtomatik doldurur.
- Timestamps UTC saxlanır; gün filtri Bakı günü UTC pəncərəsi ilə (ADR-0005).
- Hər modulun ÖZ migration tarixçəsi: `__EFMigrationsHistory` cədvəli öz schema-sında. Migration-lar startup-da tətbiq olunur (3 cəhd).
- Migration-lar əl ilə yazılır (nümunə pattern: mövcud migration + Designer + snapshot yenilənməsi birlikdə).
- Data köçürən (backfill) migration-lar mövcud sətirləri korlamamalıdır: JSON oxunuşu `ISJSON`/`TRY_CAST` ilə müdafiə olunur, sıfıra bölmə və NULL halları açıq şəkildə idarə edilir, UPDATE yalnız hələ doldurulmamış sətirlərə vurur (təkrar icra təhlükəsiz). Nümunə: `20260726142954_AddSalePurchasePricePerUnit` — sərbəst satışların alış qiymətini mövcud mayadan bərpa edir, kataloq satışlarına toxunmur (onlarda NULL qalır). Testi: `tests/MayaPro.WarehouseApi.IntegrationTests/SalesMigrationTests.cs`.
- Soft delete YOXDUR — silmələr həqiqi DELETE-dir.
- JSON sütunlar (value converter): `Product.Attributes`, `Product.Expenses`, `Sale.ExpenseItems`.
- Enum-ların saxlanması: `User.Role` string ("Owner"/"Manager"/"Seller"); `Sale.PaymentType`, `Expense.Category` — enum.

## Vacib indekslər / məhdudiyyətlər

- `identity.Users.Phone` — unique
- `dayend.Closings.Date` — unique (bir günə bir bağlanış, race qoruması)
- `activity.ActivityLogs.CreatedAt` — descending index (feed sorğusu üçün)
- `sales.Sales.InvoiceToken` — unique filtered (`IS NOT NULL`) — açıq faktura linki tokeni

## Seed (yalnız Development)

UserSeeder (4 demo istifadəçi, şifrə `demo123`), ProductSeeder, CustomerSeeder, SupplierSeeder. Sales/Expenses boş başlayır. Referans: `docs/seed.ts` (frontend seed data-sı).

## Last Updated

2026-07-26 — backfill migration qaydası (`sales.Sales.PurchasePricePerUnit`).

## Related Code

- `src/Modules/*/Infrastructure/` (DbContext, Configurations, Migrations)
- `src/MayaPro.WarehouseApi.SharedKernel/Infrastructure/` (AuditInterceptor, SqlConnectionFactory)
