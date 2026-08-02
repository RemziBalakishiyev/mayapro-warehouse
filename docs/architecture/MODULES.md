# Modules — məsuliyyətlər və asılılıqlar

11 modul. Qayda: modul başqa modulun cədvəlinə toxunmur; əlaqə yalnız `SharedKernel.Contracts` interfeysləri ilə.

| Modul | Məsuliyyət | Cədvəl sahibi? |
|---|---|---|
| **Auth** | Login, JWT, istifadəçilər/işçilər, rollar, işçi maaş hesabı | `identity.Users`, `identity.SalaryEntries` |
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
| `ISalesModule` | Sales | DayEnd (`GetDayTotalsAsync`), Reports (`GetSalesAsync`, `GetLastSaleDatesAsync`, `GetRecentSalesAsync`), Customers (`GetSalesByCustomerAsync`, `GetPurchaseStatsByCustomerAsync`, `GetOutstandingSalesAsync`, `DeleteCreditSaleAsync`), Exports (`GetInvoiceSaleAsync`, `GetSaleIdByInvoiceTokenAsync`) |
| `IExpensesModule` | Expenses | DayEnd (`GetDayTotalAsync`), Reports, Exports |
| `ISalaryModule` | Auth | DayEnd (`GetDayPaymentsTotalAsync` — günün maaş ödənişləri xərc cəminə), Reports (`GetPaymentsAsync` — dashboard `todayExpenses`/`expectedCash`). `IExpensesModule` ilə qəsdən simmetrikdir; hər iki metod YALNIZ `payment` sətirlərini qaytarır (tutulma kassaya toxunmur). |
| `ISuppliersModule` | Suppliers | Reports (ümumi borc, itemCount) |
| `IDayEndModule` | DayEnd | Reports (`GetLastClosingAsync` — ExpectedCash lövbəri), Sales (`ClosingExistsAsync` — bağlı gün qoruması) |
| `ISettingsModule` | Settings | Exports (`GetStoreNameAsync`, `GetStoreInfoAsync`) |
| `IActivityLogger` | Activity (`DbActivityLogger`) | Bütün yazan handler-lər (Products, Sales, Customers, Suppliers, Expenses, DayEnd) |

Yeni kontrakt metodu əlavə edəndə: interfeys `SharedKernel/Contracts/`-da, implementasiya provider modulun `Application/<Modul>ModuleContract.cs`-ində. Kontrakt metodları dəyişikliyi **save etmir** — caller öz UnitOfWork-ündə commit edir (bax ADR-0003).

Kontrakt record-una sahə əlavə etmək də kontrakt dəyişikliyidir: satışın alış qiyməti snapshot-u üçün `ProductStockSnapshot`-a `PurchasePrice` əlavə olundu (provider: Products, istehlakçı: Sales create/update) — modul sərhədini keçən yeganə yol budur, Sales heç vaxt `products` cədvəlini oxumur.

## Last Updated

2026-08-01 — BE#28: yeni `ISalaryModule` kontraktı (provider: Auth; istehlakçılar: DayEnd, Reports); Auth modulu `identity.SalaryEntries` cədvəlinin də sahibidir və `AuthDbContext` artıq paylaşılan transaction-a enlist olur.

2026-07-26 — `ProductStockSnapshot.PurchasePrice` kontrakt genişlənməsi.

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/` (bütün interfeyslər)
- `src/Modules/*/Application/*ModuleContract.cs` (implementasiyalar)
