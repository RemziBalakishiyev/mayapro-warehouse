# MayaPro.WarehouseApi

Anbar/ticarət idarəetməsi üçün backend API — **modular monolith** (təcrid olunmuş modullar, tək deploy).
Modullar bir-birinin cədvəlinə toxunmur; əlaqə yalnız `SharedKernel.Contracts` interfeysləri və
ortaq transaction infrastrukturu (scoped connection + unit of work) ilə qurulur.

## Stack

.NET 8 · ASP.NET Core Minimal API · EF Core 8 · SQL Server · JWT · FluentValidation · Serilog · xUnit

## Modullar

| Modul | Schema | Məsuliyyət |
|---|---|---|
| Auth | `identity` | İstifadəçilər, login, JWT, rollar (Sahibkar/Menecer/Satıcı) |
| Products | `products` | Məhsul kataloqu, real maya hesablaması, CRUD, stok düzəlişi |
| Sales | `sales` | Satış zənciri (stok → borc → satış) tək transaction-da |
| Customers | `customers` | Müştərilər, borc, ödənişlər |
| Suppliers | `suppliers` | Təchizatçılar, mal alışı borcu, ödənişlər |
| Expenses | `expenses` | Xərclər və xərc → real maya zənciri |

Ümumi təməl `SharedKernel`-dədir: `Result` pattern (exception yox), `IUnitOfWork` /
`IDbConnectionFactory` (modullararası atomik transaction), `IActivityLogger` və `IModule` mexanizmi.

## Qaydalar

- Biznes xətaları exception yox, `Result` pattern ilə; istifadəçi mesajları **Azərbaycanca**
- Bütün pul sahələri `decimal(18,2)`
- Modullar yalnız `SharedKernel.Contracts` üzərindən əlaqə saxlayır (cross-schema FK yoxdur)

## Tələblər

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- **SQL Server** (lokal instans) — connection string `appsettings.json`-dakı `ConnectionStrings:Default`;
  inteqrasiya testləri lokal SQL Server-də `MayaProWarehouse_Test` bazasını istifadə edir

## Quraşdırma və işə salma

```bash
# Build
dotnet build

# Testlər (unit + integration; integration üçün lokal SQL Server lazımdır)
dotnet test

# İşə salma (development mühitində migration-lar tətbiq olunur və demo data seed edilir)
dotnet run --project src/MayaPro.WarehouseApi.Api
```

Development mühitində Swagger UI `/swagger` ünvanında açıqdır. Demo istifadəçilər `demo123` şifrəsi ilə
seed olunur (məs. Sahibkar telefon `0501112233`).

## Struktur

```
src/
  MayaPro.WarehouseApi.Api/            # Host — modul qeydiyyatı, middleware, JWT, Swagger
  MayaPro.WarehouseApi.SharedKernel/   # Result, Contracts, UnitOfWork, IModule
  Modules/                             # Auth, Products, Sales, Customers, Suppliers, Expenses
tests/                                 # Modul unit testləri + IntegrationTests (WebApplicationFactory)
docs/                                  # Arxitektura və frontend kontrakt referansları
```
