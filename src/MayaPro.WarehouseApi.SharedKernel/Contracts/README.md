# SharedKernel.Contracts

Bu qovluq modullararası **ictimai (public) interfeyslər** üçündür. Modullar bir-birinin
cədvəlinə və ya daxili tiplərinə toxunmur — əlaqə yalnız buradakı interfeyslər üzərindən
(in-process, DI ilə) qurulur.

Nümunə (sonrakı mərhələlərdə əlavə olunacaq):

- `IProductsModule` — `TryDecreaseStockAsync(...)`, `AddExpenseToProductAsync(...)`
- `ICustomersModule` — `IncreaseDebtAsync(...)`
- `IActivityLogger` — `LogAsync(type, message, userId)`

Qaydalar:

- Yalnız DTO/record və interfeyslər; biznes məntiqi burada **QOYULMUR**.
- Hər interfeysi implement edən modul onu öz `RegisterServices`-ində DI-a bağlayır.
- Modullararası əlaqədə entity yox, sadə tiplər (Guid Id, record DTO) ötürülür.
