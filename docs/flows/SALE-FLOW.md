# Sale Flow — satış zənciri (sistemin ürəyi)

## Create (`POST /api/sales`, hər rol)

Tək transaction-da (`IUnitOfWork`):

1. Validation (FluentValidation; nisyədə `customerId` mütləq — nağd/kartda istəyə bağlıdır və satırda saxlanır, borca toxunmur).
2. Transaction açılır.
3. **Kataloq satışı** (`productId` var): `IProductsModule.TryDecreaseStockAsync` — stok çatmırsa `InsufficientStock`, hər şey geri sarılır. Qaytarılan snapshot-dan ad/kateqoriya/real maya və alış qiyməti (`ProductStockSnapshot.PurchasePrice`) götürülür.
   **Sərbəst satış** (`productId` yox): stok addımı yoxdur; ad/kateqoriya/maya/alış qiyməti (hamısı optional) command-dan; xərc sətirləri (`expenseItems`) yalnız sənədləşmə üçün saxlanır — maya hesabına girmir.
4. Nisyədirsə: `ICustomersModule.IncreaseDebtAsync(customerId, total)`.
5. `Sale.Create/CreateManual` — snapshot-larla; satıcı adı `ICurrentUser`-dan.
6. Activity log ("Satış etdi").
7. `SaveChangesAsync` (bütün enlisted context-lər) + `Commit`.

Hər hansı addım Failure qaytararsa commit-dən əvvəl return → avtomatik rollback.

## Update (`PUT /api/sales/{id}`, Owner+Manager)

Reverse-and-reapply, tək transaction:

1. Bağlı gün qoruması: `IDayEndModule.ClosingExistsAsync(satışın Bakı günü)` → varsa `Sales.DayClosedConflict` (409).
2. Köhnə effektlər geri: stok qaytarılır (`IncreaseStockAsync`), nisyə idisə borc azalır (`DecreaseDebtAsync`, 0-da floor) — best-effort.
3. Yeni dəyərlərlə CreateSale zənciri təkrar tətbiq olunur (stok çatmırsa bütün update uğursuz → rollback, köhnə vəziyyət qalır).
4. Id, tarix, satıcı dəyişmir (`ReviseCatalogued`/`ReviseManual`).
5. Snapshot-lar (maya, alış qiyməti, ad, kateqoriya) yenidən müəyyən olunur: kataloq satışında məhsulun CARİ dəyərlərindən, sərbəst satışda command-dan — command-da göndərilməyən alış qiyməti `null` yazılır (köhnə dəyər saxlanmır).

## Delete (`DELETE /api/sales/{id}`, Owner+Manager)

1. Stok qaytarılır (kataloq satışında), nisyə idisə borc azalır (0-da floor) — best-effort (məhsul/müştəri silinibsə ötürülür).
2. Sətir silinir + activity log. Bağlı gün qoruması YOXDUR (bilinçli — bax handler şərhi).

Nisyə sətrini müştəri borc UI-dan silmək: `DELETE /api/customers/{id}/credits/{saleId}` → `ISalesModule.DeleteCreditSaleAsync` (sale həmin müştəriyə aid nisyə olmalıdır) → eyni DeleteSale zənciri.

## Oxuma

`GET /api/sales` — pagination + tarix filtri (Bakı günü pəncərələri). `GET /api/sales/{id}` — detal: müştəri adı + məhsulun cari adı (snapshot-dan fərqli ola bilər) + xərc sətirləri.

## Last Updated

2026-07-26 — alış qiyməti snapshot-u (create + update).

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/` (CreateSale, UpdateSale, DeleteSale, GetSales, GetSaleById)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
