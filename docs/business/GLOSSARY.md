# Glossary — Biznes Terminləri

Bazar (Sədərək) anbar-satış konteksti. Wire-dakı Azərbaycanca dəyərlər API kontraktının bir hissəsidir və dəyişdirilə bilməz (`SharedKernel/Contracts/WireFormat.cs`).

| Termin | Mənası |
|---|---|
| **Nağd** | Nağd ödənişli satış (wire: `paymentType = "Nağd"`) |
| **Kart** | Kartla ödənişli satış (wire: `"Kart"`) |
| **Nisyə** | Kredit satış — pul sonra ödənilir, müştərinin borcu artır (wire: `"Nisyə"`). Yalnız nisyə satışda `customerId` olur |
| **Borc (Debt)** | Müştərinin ödənilməmiş qalığı. Yalnız domain metodları ilə dəyişir, 0-dan aşağı düşmür |
| **İlkin borc (InitialDebt)** | Müştəri yaradılarkən köçürülən başlanğıc borc |
| **Real maya (RealCostPerUnit)** | Bir vahidin həqiqi maya dəyəri = alış qiyməti + (partiya xərcləri ÷ ilkin say). `Product.CalculateRealCost` |
| **İlkin say (InitialQuantity)** | Məhsul yaradılarkən alınan partiya sayı; ömürlük sabit — xərc bölgüsünün məxrəci |
| **Sərbəst satış (manual sale)** | Kataloqda olmayan malın əl ilə yazılmış satışı: `productId = null`, stok hərəkəti yoxdur, maya bilinməyə bilər (`IsManual`) |
| **Snapshot** | Satış anında məhsul adı/kateqoriya/maya kopyalanır — məhsul sonradan dəyişsə tarixi qazanc pozulmur |
| **Qazanc (Profit)** | (satış qiyməti − maya) × say. Maya bilinməyəndə `null` — hesabatlar 0 kimi yox, "naməlum" sayır |
| **Qaimə-faktura** | Satış üçün A5 PDF sənəd, № formatı `SF-yyyyMMdd-XXXXXX` (Exports modulu) |
| **Bağlanış (Closing)** | Gün sonu kassa üzləşdirməsi. `ExpectedCash = OpeningCash + CashSales − Expenses`; `Difference = ActualCash − ExpectedCash` |
| **Dondurulmuş mal (frozen stock)** | 30/60/90 gün satılmayan məhsullar (Reports) |
| **Sahibkar (sahib)** | Mağaza sahibi rolu — tam səlahiyyət (wire: `role = "sahib"`) |
| **Satıcı (satici)** | Satış edən işçi rolu — məhdud səlahiyyət (wire: `"satici"`) |
| **Menecer** | Wire formatda mövcud rol dəyəri (`"menecer"`) |
| **Xərc kateqoriyaları** | Yol, Fəhlə, Anbar/Yer, Paket/Qutu, Mağaza, Digər (wire dəyərləri) |
| **WhatsApp şablonu** | Settings-də borc xatırlatma mesajı şablonu; `{debt}` placeholder-ini frontend əvəz edir. Backend mesaj göndərmir |
| **Açıq faktura linki** | Satışın sabit tokenli auth-suz PDF linki (`/api/public/invoices/{token}`) — WhatsApp-la paylaşmaq üçün; IP başına 30/dəq limit |

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/WireFormat.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.DayEnd/Domain/Closing.cs`
