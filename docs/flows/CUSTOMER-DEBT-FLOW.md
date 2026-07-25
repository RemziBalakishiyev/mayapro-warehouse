# Customer Debt Flow — müştəri borcu

## Borcun artması

- **Nisyə satış**: CreateSale zənciri `IncreaseDebtAsync(customerId, total)` çağırır (bax SALE-FLOW).
- **İlkin borc**: müştəri yaradılarkən `initialDebt > 0` göndərilibsə borc + `CustomerDebtAdjustment` tarixçə sətri yazılır.

## Borcun azalması

- **Ödəniş** (`POST /api/customers/{id}/payments`, hər rol): `Customer.DecreaseDebt` — məbləğ borcdan böyükdürsə `Customers.PaymentExceedsDebt` (400). `CustomerPayment` sətri yazılır, activity log.
- **Nisyə satışın silinməsi/düzəlişi**: `ReverseDebt` — 0-da floor, heç vaxt imtina etmir.

## Borc tarixçəsi (`GET /api/customers/{id}/history`)

Üç mənbə birləşdirilir, xronoloji sıralanır:
1. `CustomerDebtAdjustments` (ilkin borc) — type `InitialDebt`
2. Nisyə satışlar — Sales kontraktından (`GetCreditSalesByCustomerAsync`), type `Sale`, `saleId` ilə
3. `CustomerPayments` — type `Payment`

## Nisyə sətrinin silinməsi (`DELETE /api/customers/{id}/credits/{saleId}`, Owner+Manager)

Customers modulu müştərini yoxlayır → `ISalesModule.DeleteCreditSaleAsync(saleId, customerId)` — satış həmin müştəriyə aid nisyə deyilsə `Customers.CreditSaleNotFound`. Uğurda tam DeleteSale zənciri işləyir (stok qayıdır, borc azalır).

## Müştərinin silinməsi (`DELETE /api/customers/{id}`, yalnız Owner)

Borc qalsa belə silinir. Ödənişlər + ilkin borc sətirləri də silinir (tək transaction). Satışlar toxunulmur — `CustomerId` qalır, ad axtarışı boş qayıdır → frontend "Silinmiş müştəri" göstərir.

## WhatsApp xatırlatması

Backend mesaj göndərmir. `StoreSettings.WhatsappTemplate` şablonunu saxlayır (`{debt}` placeholder); frontend borcu əvəz edib WhatsApp linki açır.

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Customers/` (Domain/Customer.cs, UseCases/)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/SalesModuleContract.cs` (DeleteCreditSaleAsync)
