# Database

Tək SQL Server DB (`MayaProWarehouse`), connection string: `ConnectionStrings:Default`. Test DB: `MayaProWarehouse_Test` (integration testlər lokal SQL Server-də).

## Schema-per-module

| Schema | DbContext | Cədvəllər | Transactional* |
|---|---|---|---|
| `identity` | AuthDbContext | Users, SalaryEntries | ✅ |
| `products` | ProductsDbContext | Products, Categories | ✅ |
| `sales` | SalesDbContext | Sales | ✅ |
| `customers` | CustomersDbContext | Customers, CustomerPayments, CustomerDebtAdjustments | ✅ |
| `suppliers` | SuppliersDbContext | Suppliers, SupplierPayments, SupplierDebtAdjustments | ✅ |
| `expenses` | ExpensesDbContext | Expenses, ExpenseTypes | ✅ |
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
- Data köçürən (backfill) migration-lar mövcud sətirləri korlamamalıdır: JSON oxunuşu `ISJSON`/`TRY_CAST` ilə müdafiə olunur, sıfıra bölmə və NULL halları açıq şəkildə idarə edilir, UPDATE yalnız hələ doldurulmamış sətirlərə vurur (təkrar icra təhlükəsiz). Nümunə: `20260726142954_AddSalePurchasePricePerUnit` — sərbəst satışların alış qiymətini mövcud mayadan bərpa edir, kataloq satışlarına toxunmur (onlarda NULL qalır). `20260730142515_AddSalePaidAmount` — nağd/kart satışlara `PaidAmount = TotalAmount`, `PaidVia = PaymentType` yazır; nisyə sətirlər sütun default-larında (0 / Cash) qalır, yəni heç toxunulmur. UPDATE `WHERE PaymentType <> 'Credit' AND PaidAmount = 0` ilə qorunur — miqrasiyadan sonra yazılan heç bir sətir bu şərtə düşmür (qismən ödənilmiş satış həmişə Credit, saxlanan nağd/kart satış isə tam ödənilib), ona görə təkrar icra real ödəniş məlumatını silmir. Testi: `tests/MayaPro.WarehouseApi.IntegrationTests/SalesMigrationTests.cs`.
- Soft delete YOXDUR — silmələr həqiqi DELETE-dir.
- JSON sütunlar (value converter): `Product.Attributes`, `Product.Expenses`, `Sale.ExpenseItems`.
- Enum-ların saxlanması: `User.Role` string ("Owner"/"Manager"/"Seller"); `SalaryEntry.Type` — enum, string kimi ("Payment"/"Deduction", `nvarchar(20)`); `Sale.PaymentType` və `Sale.PaidVia` — enum, string kimi (`nvarchar(20)`: "Cash"/"Card"/"Credit"). `Sale.PaidAmount` — `decimal(18,2)`, NOT NULL (BE#15; `RemainingAmount = TotalAmount − PaidAmount` sütun deyil, hesablanır). `Expense.Category` artıq enum DEYİL — idarə olunan `ExpenseType.Name`-in sərbəst-string snapshot-u (`nvarchar(100)`, FK yoxdur). `Expense.Source` (general/product) — enum, string kimi saxlanır.

## Vacib indekslər / məhdudiyyətlər

- `identity.Users.Phone` — unique
- `identity.SalaryEntries.Date` — non-unique (gün sonu / dashboard kassa sorğusu); `(UserId, Month)` — non-unique (aylıq maaş xülasəsi). `UserId` FK DEYİL (`Expense.ProductId` ilə eyni yanaşma).
- `dayend.Closings.Date` — unique (bir günə bir bağlanış, race qoruması)
- `activity.ActivityLogs.CreatedAt` — descending index (feed sorğusu üçün)
- `sales.Sales.InvoiceToken` — unique filtered (`IS NOT NULL`) — açıq faktura linki tokeni
- `customers.CustomerDebtAdjustments.CustomerId`, `suppliers.SupplierDebtAdjustments.SupplierId` — non-unique (tarixçə sorğusu üçün; ödəniş cədvəllərində də eyni)

## Seed (yalnız Development)

UserSeeder (4 demo istifadəçi, şifrə `demo123`), ProductSeeder, CustomerSeeder, SupplierSeeder, ExpenseTypeSeeder (7 default xərc növü). Sales/Expenses boş başlayır. Referans: `docs/seed.ts` (frontend seed data-sı).

## Last Updated

2026-08-01 — BE#28: `identity.Users.MonthlySalary` sütunu (`decimal(18,2)` NOT NULL DEFAULT 0) + yeni `identity.SalaryEntries` cədvəli (migration `EmployeeSalaryAndSalaryEntries`). `AuthDbContext` öz connection string-indən paylaşılan `IDbConnectionFactory` bağlantısına keçdi və `ITransactionalDbContext` oldu — maaş sətri ilə activity log-u eyni transaction-da yazmaq üçün. `SalaryEntry.Date` (pulun kassadan çıxdığı UTC anı) və `SalaryEntry.Month` (`yyyy-MM`, hansı ayın hesabına) AYRI sahələrdir və bir-birini əvəz etmir.

2026-07-30 — BE#15: `sales.Sales.PaidAmount` + `PaidVia` sütunları (migration `AddSalePaidAmount`, backfill + re-run qorunması).

2026-07-27 — `expenses.ExpenseTypes` cədvəli, `Expense.Category` enum→string, `Expense.Source` sütunu (migration `ExpenseTypesAndSource`, BE#4); `suppliers.SupplierDebtAdjustments` cədvəli (`AddSupplierDebtAdjustments`).

## Related Code

- `src/Modules/*/Infrastructure/` (DbContext, Configurations, Migrations)
- `src/MayaPro.WarehouseApi.SharedKernel/Infrastructure/` (AuditInterceptor, SqlConnectionFactory)
