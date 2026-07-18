# Sədərək Sistem — Backend Arxitekturası

**Stack:** .NET 8 + ASP.NET Core (Minimal API) + EF Core 8 + SQL Server + JWT Auth

**Üslub:** Modular Monolith — tək deploy olunan proqram, amma daxildə frontend-dəki feature-lərlə üst-üstə düşən, bir-birindən təcrid olunmuş modullar. Hər modul öz domain-ini, öz cədvəllərini (ayrıca SQL schema) və öz endpoint-lərini idarə edir. Sabah bir modul (məs. Reports) ağırlaşarsa, mikroservisə çıxarmaq minimal ağrı ilə mümkün olur.

**Ən vacib prinsip:** Frontend-in `mocks/handlers.ts` faylı artıq backend-in davranış spesifikasiyasıdır. Hər handler funksiyası bir use case-ə, hər `api.ts` funksiyası bir endpoint-ə çevrilir. Biznes zəncirləri (satış → stok azalır → borc artır) frontend-dən silinib bura köçür.

---

## 1. Solution strukturu

```
Sederek.sln
│
├── src/
│   ├── Sederek.Api/                      # ⭐ Host — tək giriş nöqtəsi
│   │   ├── Program.cs                    # modul qeydiyyatı, middleware pipeline
│   │   ├── appsettings.json
│   │   └── Extensions/                   # AddModules(), UseModules()
│   │
│   ├── Sederek.SharedKernel/             # Modullararası ortaq təməl (bura biznes məntiqi QOYULMUR)
│   │   ├── Domain/
│   │   │   ├── Entity.cs                 # Id, CreatedAt, audit sahələri
│   │   │   ├── IDomainEvent.cs
│   │   │   └── Money.cs                  # decimal wrapper (istəyə bağlı, sadə saxlamaq olar)
│   │   ├── Application/
│   │   │   ├── Result.cs                 # Result / Result<T> — exception əvəzinə
│   │   │   ├── Error.cs                  # kod + Azərbaycanca mesaj
│   │   │   ├── IUnitOfWork.cs
│   │   │   └── PagedResult.cs
│   │   ├── Infrastructure/
│   │   │   ├── IModule.cs                # hər modulun implement etdiyi interfeys
│   │   │   └── AuditInterceptor.cs       # CreatedAt/UpdatedAt avtomatik
│   │   └── Contracts/                    # ⭐ modullararası "ictimai" interfeyslər
│   │       ├── IProductsModule.cs        # məs. DecreaseStockAsync(...)
│   │       ├── ICustomersModule.cs       # məs. IncreaseDebtAsync(...)
│   │       └── IActivityLogger.cs        # LogAsync(type, message, userId)
│   │
│   └── Modules/
│       ├── Sederek.Modules.Identity/     # login, işçilər, rollar (frontend: auth + employees)
│       ├── Sederek.Modules.Products/
│       ├── Sederek.Modules.Sales/
│       ├── Sederek.Modules.Customers/
│       ├── Sederek.Modules.Suppliers/
│       ├── Sederek.Modules.Expenses/
│       ├── Sederek.Modules.DayEnd/
│       ├── Sederek.Modules.Reports/      # yalnız oxuyur — öz cədvəli yoxdur
│       ├── Sederek.Modules.Settings/
│       └── Sederek.Modules.Activity/     # activity log — hamı bura yazır (IActivityLogger ilə)
│
└── tests/
    ├── Sederek.Modules.Sales.Tests/      # ən kritik: satış zənciri unit testləri
    ├── Sederek.Modules.Products.Tests/
    └── Sederek.IntegrationTests/         # WebApplicationFactory + Testcontainers (SQL Server)
```

### Hər modulun daxili quruluşu (nümunə: Sales)

```
Sederek.Modules.Sales/
├── Domain/
│   ├── Sale.cs                    # entity + davranış (məs. static Create(...) factory)
│   └── SaleErrors.cs              # Error.InsufficientStock və s.
├── Application/
│   ├── Abstractions/
│   │   └── ISalesDbContext.cs     # yalnız bu modulun DbSet-ləri
│   └── UseCases/
│       ├── CreateSale/
│       │   ├── CreateSaleCommand.cs      # record: məhsul, say, qiymət, endirim, ödəniş növü...
│       │   ├── CreateSaleHandler.cs      # ⭐ biznes zənciri burada
│       │   └── CreateSaleValidator.cs    # FluentValidation
│       ├── GetSales/
│       └── GetTodaySales/
├── Infrastructure/
│   ├── SalesDbContext.cs          # schema: "sales"
│   ├── Configurations/
│   │   └── SaleConfiguration.cs   # IEntityTypeConfiguration — decimal(18,2), indexlər
│   └── Migrations/                # hər modulun ÖZ migration tarixçəsi
├── Endpoints/
│   └── SalesEndpoints.cs          # MapGroup("/api/sales") — minimal API
└── SalesModule.cs                 # IModule: servis qeydiyyatı + endpoint map + migrate
```

---

## 2. Əsas arxitektura qərarları

### 2.1 Modul sərhədləri: schema-per-module, single database

Hər modulun **öz DbContext-i** və SQL Server-də **öz schema-sı** var: `products.Products`, `sales.Sales`, `customers.Customers`, `activity.ActivityLogs`... Qaydalar:

- Bir modul başqa modulun cədvəlinə **SQL səviyyəsində toxunmur** (JOIN yox, FK yox — modullararası əlaqə yalnız Id saxlamaqla: `Sale.CustomerId` sadəcə Guid-dir, navigation property deyil)
- Modullararası çağırış yalnız `SharedKernel.Contracts`-dakı interfeyslərlə (in-process, DI üzərindən)
- Reports modulu istisnadır: read-only olduğu üçün ona xüsusi icazə — ya hər modulun public query servisini çağırır, ya da ayrıca read-only DbContext ilə view-lardan oxuyur (tövsiyə: birinci variantla başla, yavaşlasa view-lara keç)

### 2.2 Satış zənciri — sistemin ürəyi necə işləyir

Frontend-dəki `saleHandlers.createSale` bura belə köçür:

```csharp
// CreateSaleHandler.cs (sadələşdirilmiş)
public async Task<Result<SaleDto>> Handle(CreateSaleCommand cmd, CancellationToken ct)
{
    // Modullararası əməliyyat tək transaction-da:
    await using var tx = await _uow.BeginTransactionAsync(ct);

    // 1. Stok yoxla + azalt (Products modulunun public kontraktı)
    var stock = await _products.TryDecreaseStockAsync(cmd.ProductId, cmd.Quantity, ct);
    if (stock.IsFailure)
        return Result.Failure<SaleDto>(SaleErrors.InsufficientStock); // "Stokda kifayət qədər mal yoxdur"

    // 2. Nisyədirsə müştəri borcunu artır (net məbləğ — endirimdən sonra!)
    if (cmd.PaymentType == PaymentType.Nisye)
        await _customers.IncreaseDebtAsync(cmd.CustomerId!.Value, sale.TotalAmount, ct);

    // 3. Satışı yaz (maya snapshot-u ilə — sonradan maya dəyişsə tarixi qazanc pozulmasın)
    var sale = Sale.Create(cmd, costSnapshot: stock.Value.RealCost);
    _db.Sales.Add(sale);

    // 4. Activity log
    await _activity.LogAsync(ActivityType.Sale, $"{sale.ProductName} × {sale.Quantity}", cmd.UserId, ct);

    await _uow.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return Result.Success(sale.ToDto());
}
```

Qeyd: modullar ayrı DbContext-lərdə olsa da, **eyni database** olduğu üçün hamısı bir `IDbContextTransaction`-ı paylaşa bilir (SharedKernel-dəki UnitOfWork bunu koordinasiya edir). Bu, modular monolith-in mikroservis üzərindəki ən böyük üstünlüyüdür — distributed transaction problemi yoxdur.

Eyni pattern digər zəncirlərdə: `AddCustomerPayment` (borc azalır, 0-dan aşağı yox), `CreateExpense` (mala bağlıdırsa → `IProductsModule.AddExpenseToProductAsync` → real maya yenidən hesablanır), `CloseDay` (eyni tarixə ikinci bağlanış → xəta).

### 2.3 Use case üslubu: sadə handler-lər, MediatR-siz

Hər use case bir qovluq: Command/Query + Handler + Validator. MediatR əlavə etmirik — DI ilə birbaşa handler inject olunur (`endpoint → handler`). Layihə bu ölçüdə pipeline behavior mürəkkəbliyinə dəyməz; lazım olsa sonra əlavə etmək asandır.

### 2.4 Result pattern — exception-suz biznes xətaları

Biznes qaydası pozulanda exception atılmır, `Result.Failure(Error)` qayıdır. Error-larda **Azərbaycanca mesajlar** — frontend toast-ları birbaşa göstərir:

```csharp
public static class SaleErrors
{
    public static readonly Error InsufficientStock =
        new("Sales.InsufficientStock", "Stokda kifayət qədər mal yoxdur");
    public static readonly Error CustomerRequired =
        new("Sales.CustomerRequired", "Nisyə satış üçün müştəri seçilməlidir");
}
```

Endpoint-lərdə vahid çevirmə: `Result` → 200/400/404, body həmişə eyni formatda: `{ "code": "...", "message": "..." }`. Frontend `api-client.ts` bu formatı tanıyıb toast göstərəcək.

### 2.5 Auth və rollar

- **JWT** (access token ~1 gün — bazar konteksti üçün kifayətdir, refresh token sonra əlavə oluna bilər)
- Identity modulu: `Users` cədvəli (telefon/email + BCrypt hash), rol enum: `Sahibkar`, `Menecer`, `Satici`
- Endpoint-lərdə policy-lər: məs. `RequireRole(Sahibkar)` → gün sonu, ayarlar, işçilər; `Sahibkar|Menecer` → mal əlavə/redaktə, xərclər; hamı → satış, baxış
- Frontend-dəki Ayarlar səhifəsindəki "İcazələr" kartı bu policy-lərin əksidir — backend hazır olanda `features/auth/store.ts`-ə `can(permission)` əlavə olunub UI düymələri gizlədiləcək

### 2.6 API kontraktı — frontend-lə üz-üzə

Frontend-in hər `api.ts`-i bir endpoint qrupudur. Ümumi qaydalar: bütün route-lar `/api/...`, camelCase JSON, tarixlər ISO 8601, pul `decimal` (JSON-da number).

| Modul | Endpoint-lər (əsas) |
|---|---|
| Identity | `POST /api/auth/login`, `GET /api/auth/me`, `GET /api/employees` |
| Products | `GET/POST /api/products`, `GET/PUT /api/products/{id}`, `POST /api/products/{id}/adjust-stock` |
| Sales | `GET /api/sales?date=&from=&to=&take=50&skip=0` (PagedResult), `POST /api/sales` |
| Customers | `GET/POST /api/customers`, `GET /api/customers/{id}`, `POST /api/customers/{id}/payments`, `GET /api/customers/{id}/payments` |
| Suppliers | eyni pattern + `POST /api/suppliers/{id}/debts` |
| Expenses | `GET /api/expenses?month=`, `POST /api/expenses` |
| DayEnd | `GET /api/closings`, `GET /api/closings/today`, `POST /api/closings` |
| Reports | `GET /api/reports/dashboard`, `GET /api/reports/summary?period=` |
| Settings | `GET/PUT /api/settings` |
| Activity | `GET /api/activity?take=50&skip=0` |

Reports endpoint-ləri frontend-dəki `computeDashboardStats`-ın qaytardığı formada hazır DTO verir — hesablama serverə köçür, frontend yalnız göstərir.

### 2.7 EF Core qaydaları

- Bütün pul sahələri `decimal(18,2)` (global convention ilə)
- `Product.Expenses` breakdown-u owned entity kimi (`OwnsOne`) — ayrı cədvəl yox, sütunlar: `Expenses_Yol`, `Expenses_Fehle`...
- İndekslər: `Sales(Date)`, `Sales(CustomerId)`, `Products(Barcode) unique filtered`, `ActivityLogs(CreatedAt DESC)`
- Soft delete YOX (MVP-də silmə yoxdur) — sadəlik qorunur
- Migration-lar modul-daxili; startup-da hər modul öz migration-ını tətbiq edir (development), production-da CI script ilə
- Seed: development mühitində frontend `seed.ts`-dəki data ilə eyni seeder (demo/test üçün)

### 2.8 Kəsişən qayğılar

- **Validation:** FluentValidation, endpoint filter kimi — Azərbaycanca mesajlarla
- **Logging:** Serilog (console + rolling file), request logging middleware
- **Swagger/OpenAPI:** development-də açıq — frontend inteqrasiyasında canlı sənəd
- **CORS:** frontend origin-i konfiqurasiyadan
- **Global exception handler:** gözlənilməz xətalar → 500 + generic Azərbaycanca mesaj, detallar loga
- **Concurrency:** `Product.RowVersion` (rowversion) — iki satıcı eyni anda eyni malı satanda stok düzgün qalsın; konflikt → retry (handler daxilində 1 dəfə təkrar cəhd)

---

## 3. Frontend-ə qoşulma planı

Frontend bu günə hazırdır: `.env`-ə `VITE_API_URL=https://localhost:7xxx` yazılan kimi mock sönür. Backend tərəfdə uyğunluq üçün:

1. Response formatları frontend tiplərinə (`src/types/index.ts`) uyğunlaşdırılır — TS tipləri əslində DTO spesifikasiyasıdır
2. `api-client.ts`-ə JWT header + 401 → `/login` redirect əlavə olunur
3. Keçid modulu-modul edilə bilər: `USE_MOCK` yoxlamasını feature-səviyyəsində parçalayıb əvvəlcə yalnız products-u real API-yə keçirmək mümkündür

---

## 4. Qurulma ardıcıllığı (Claude Code sessiyaları)

1. **Skelet:** solution, SharedKernel (Result, Error, IModule, UnitOfWork), Api host, boş modul qeydiyyat mexanizmi, Serilog, Swagger, SQL Server connection
2. **Identity:** users cədvəli, login, JWT, seed işçilər, rol policy-ləri
3. **Products:** entity + real maya domain məntiqi, CRUD + adjust-stock, migration, seed
4. **Sales + Customers (minimum):** satış zənciri tək transaction-da, modullararası kontraktlar işə düşür — arxitekturanın əsl sınağı
5. **Customers tam + Suppliers:** ödənişlər, borclar
6. **Expenses + DayEnd:** xərc→maya kontraktı, bağlanış qaydası
7. **Reports + Activity + Settings:** dashboard DTO-ları
8. **Frontend inteqrasiyası:** CORS, api-client yenilənməsi, modulbamodul keçid, integration testlər

---

## 5. Konvensiyalar

- Namespace = qovluq strukturu; hər modulun public üzü yalnız `Endpoints` + `SharedKernel.Contracts` implementasiyası
- Command/Query-lər `record`, DTO-lar `record`, entity-lər davranışlı class (public setter-lərdən qaç)
- Bütün istifadəçiyə gedən mesajlar Azərbaycanca, log mesajları ingiliscə
- `dotnet format` + `.editorconfig`; hər mərhələ ayrıca commit, testlər yaşıl olmadan commit yox
