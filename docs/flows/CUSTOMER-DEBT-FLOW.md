# Customer Debt Flow — müştəri borcu

## Borcun artması

- **Nisyə satış**: CreateSale zənciri `IncreaseDebtAsync(customerId, total)` çağırır (bax SALE-FLOW).
- **İlkin borc**: müştəri yaradılarkən `initialDebt > 0` göndərilibsə borc + `CustomerDebtAdjustment` tarixçə sətri yazılır.

## Borcun azalması

- **Ödəniş** (`POST /api/customers/{id}/payments`, hər rol): `Customer.DecreaseDebt` — məbləğ borcdan böyükdürsə `Customers.PaymentExceedsDebt` (400). `CustomerPayment` sətri yazılır, activity log.
- **Nisyə satışın silinməsi/düzəlişi**: `ReverseDebt` — 0-da floor, heç vaxt imtina etmir.

## Müştəri tarixçəsi (`GET /api/customers/{id}/history`)

Üç mənbə birləşdirilir, xronoloji sıralanır:
1. `CustomerDebtAdjustments` (ilkin borc) — type `initialDebt`
2. BÜTÜN satışlar (hər ödəniş növü) — Sales kontraktından (`GetSalesByCustomerAsync`), type `sale`, `saleId` + `paymentType` ilə. Borcu yalnız Nisyə sətirləri artırıb; frontend `paymentType` ilə fərqləndirir
3. `CustomerPayments` — type `payment`

## Açıq borclar (`GET /api/customers/open-debts`, BE#21)

Bütün müştərilərin hələ bağlanmamış borc mənbələri, ən köhnəsi əvvəldə. Mənbə iki cürdür:
1. `CustomerDebtAdjustments` (ilkin borc) — `source` = `initialDebt`, `description` = «İlkin borc»
2. Qalıqlı satışlar — `ISalesModule.GetOutstandingSalesAsync` (`TotalAmount − PaidAmount > 0` VƏ `CustomerId` dolu olan bütün satışlar, ödəniş növündən asılı olmayaraq); `originalAmount` satışın YEKUNU deyil, borc yaradan qalığıdır.

Bölgü qaydası **FIFO**: müştərinin ödənişlərinin cəmi mənbələr üzərində tarix artan sırada silinir; `remaining = 0` olan mənbə siyahıya düşmür. Ödəniş cəmi ilə işləmək ödənişləri bir-bir tətbiq etməklə eyni nəticəni verir, çünki ödəniş heç vaxt o andakı borcdan çox ola bilmir (`Customer.DecreaseDebt`). Hesablama sorğu anında aparılır — ayrıca cədvəl saxlanmır — və müştəri sayından asılı olmayaraq dörd sorğudur (müştərilər, ilkin borclar, qruplaşdırılmış ödəniş cəmləri, qalıqlı satışlar).

Yoxlama: bir müştərinin sətirlərindəki `remaining` cəmi onun `Customer.Debt` sahəsinə bərabər olmalıdır. Fərq varsa sorğu uğurlu qalır (istifadəçi siyahını görür), fərq isə warning kimi log-a yazılır — bu, data keyfiyyəti siqnalıdır, biznes xətası deyil. Silinmiş müştəriyə aid qalmış satış sətirləri (FK yoxdur) siyahıdan çıxarılır.

## Müştəri statistikaları (`GET /api/customers`)

`ISalesModule.GetPurchaseStatsByCustomerAsync` — bütün satış növləri üzərində qruplaşdırılmış tək sorğu: `lastPurchaseDate` (son istənilən satış), `totalPurchases`, `purchaseCount`.

## Nisyə sətrinin silinməsi (`DELETE /api/customers/{id}/credits/{saleId}`, Owner+Manager)

Customers modulu müştərini yoxlayır → `ISalesModule.DeleteCreditSaleAsync(saleId, customerId)` — satış həmin müştəriyə aid nisyə deyilsə `Customers.CreditSaleNotFound`. Uğurda tam DeleteSale zənciri işləyir (stok qayıdır, borc azalır).

## Müştərinin silinməsi (`DELETE /api/customers/{id}`, yalnız Owner)

Borc qalsa belə silinir. Ödənişlər + ilkin borc sətirləri də silinir (tək transaction). Satışlar toxunulmur — `CustomerId` qalır, ad axtarışı boş qayıdır → frontend "Silinmiş müştəri" göstərir.

## WhatsApp xatırlatması

Backend mesaj göndərmir. `StoreSettings.WhatsappTemplate` şablonunu saxlayır (`{debt}` placeholder); frontend borcu əvəz edib WhatsApp linki açır.

## Last Updated

2026-08-01 — açıq borclar (FIFO) bölməsi əlavə olundu (BE#21).

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Customers/` (Domain/Customer.cs, UseCases/)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/SalesModuleContract.cs` (DeleteCreditSaleAsync)
