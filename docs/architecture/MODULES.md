# Modules — məsuliyyətlər və asılılıqlar

11 modul. Qayda: modul başqa modulun cədvəlinə toxunmur; əlaqə yalnız `SharedKernel.Contracts` interfeysləri ilə.

| Modul | Məsuliyyət | Cədvəl sahibi? |
|---|---|---|
| **Auth** | Login, JWT, istifadəçilər/işçilər, rollar | `identity.Users` |
| **Products** | Məhsul kataloqu, stok, real maya, kateqoriyalar | `products.*` |
| **Sales** | Satış zənciri (create/update/delete), satış tarixçəsi | `sales.Sales` |
| **Customers** | Müştərilər, borc, ödənişlər, borc tarixçəsi | `customers.*` |
| **Suppliers** | Təchizatçılar, təchizatçı borcu/ödənişləri | `suppliers.*` |
| **Expenses** | Xərclər, xərc→məhsul maya zənciri | `expenses.Expenses` |
| **DayEnd** | Gün sonu kassa bağlanışı | `dayend.Closings` |
| **Activity** | Sistem hərəkət jurnalı (`IActivityLogger` implementasiyası) | `activity.ActivityLogs` |
| **Settings** | Mağaza parametrləri (singleton sətir) | `settings.StoreSettings` |
| **Reports** | Dashboard + dövr xülasəsi — yalnız oxuyur | yoxdur |
| **Exports** | Excel/PDF/faktura faylları — yalnız oxuyur | yoxdur |

## Kontraktlar: kim verir, kim istifadə edir

| Kontrakt | Provider | İstehlakçılar (əsas metodlar) |
|---|---|---|
| `IProductsModule` | Products | Sales (`TryDecreaseStockAsync`, `IncreaseStockAsync`), Expenses (`AddExpenseToProductAsync`, `GetSnapshotAsync`), Reports, Exports |
| `ICustomersModule` | Customers | Sales (`IncreaseDebtAsync`, `DecreaseDebtAsync`), Reports (`GetTotalDebtAsync`, `GetNamesAsync`, `GetRecentPaymentsAsync`), Exports (`GetCustomerInfoAsync`) |
| `ISalesModule` | Sales | DayEnd (`GetDayTotalsAsync`), Reports (`GetSalesAsync`, `GetLastSaleDatesAsync`, `GetRecentSalesAsync`), Customers (`GetCreditSalesByCustomerAsync`, `DeleteCreditSaleAsync`, `GetLastCreditSaleDatesByCustomerAsync`), Exports (`GetInvoiceSaleAsync`) |
| `IExpensesModule` | Expenses | DayEnd (`GetDayTotalAsync`), Reports, Exports |
| `ISuppliersModule` | Suppliers | Reports (ümumi borc, itemCount) |
| `IDayEndModule` | DayEnd | Reports (`GetLastClosingAsync` — ExpectedCash lövbəri), Sales (`ClosingExistsAsync` — bağlı gün qoruması) |
| `ISettingsModule` | Settings | Exports (`GetStoreNameAsync`, `GetStoreInfoAsync`) |
| `IActivityLogger` | Activity (`DbActivityLogger`) | Bütün yazan handler-lər (Products, Sales, Customers, Suppliers, Expenses, DayEnd) |

Yeni kontrakt metodu əlavə edəndə: interfeys `SharedKernel/Contracts/`-da, implementasiya provider modulun `Application/<Modul>ModuleContract.cs`-ində. Kontrakt metodları dəyişikliyi **save etmir** — caller öz UnitOfWork-ündə commit edir (bax ADR-0003).

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/` (bütün interfeyslər)
- `src/Modules/*/Application/*ModuleContract.cs` (implementasiyalar)
