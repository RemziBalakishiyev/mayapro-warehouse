# Glossary — Biznes Terminləri

Bazar (Sədərək) anbar-satış konteksti. Wire-dakı Azərbaycanca dəyərlər API kontraktının bir hissəsidir və dəyişdirilə bilməz (`SharedKernel/Contracts/WireFormat.cs`).

| Termin | Mənası |
|---|---|
| **Nağd** | Nağd ödənişli satış (wire: `paymentType = "Nağd"`) |
| **Kart** | Kartla ödənişli satış (wire: `"Kart"`) |
| **Nisyə** | Kredit satış — pul sonra ödənilir, müştərinin borcu artır (wire: `"Nisyə"`). `customerId` nisyədə məcburidir; nağd/kartda da istəyə bağlı göndərilə bilər (borca təsirsiz) |
| **Borc (Debt)** | Müştərinin ödənilməmiş qalığı. Yalnız domain metodları ilə dəyişir, 0-dan aşağı düşmür |
| **İlkin borc (InitialDebt)** | Müştəri yaradılarkən köçürülən başlanğıc borc |
| **Real maya (RealCostPerUnit)** | Bir vahidin həqiqi maya dəyəri = alış qiyməti + (partiya xərcləri ÷ ilkin say). `Product.CalculateRealCost` |
| **İlkin say (InitialQuantity)** | Məhsul yaradılarkən alınan partiya sayı; ömürlük sabit — xərc bölgüsünün məxrəci |
| **Sərbəst satış (manual sale)** | Kataloqda olmayan malın əl ilə yazılmış satışı: `productId = null`, stok hərəkəti yoxdur, maya bilinməyə bilər (`IsManual`) |
| **Alış qiyməti (PurchasePricePerUnit)** | Satış anında 1 vahidin TƏMİZ alış qiyməti (xərcsiz) — mayadan ayrıca saxlanılan snapshot. Qazanc hesabına girmir; bilinmirsə `null`. `Sale.PurchasePricePerUnit` |
| **Snapshot** | Satış anında məhsul adı/kateqoriya/maya/alış qiyməti kopyalanır — məhsul sonradan dəyişsə tarixi qazanc pozulmur |
| **Qazanc (Profit)** | (satış qiyməti − maya) × say. Maya bilinməyəndə `null` — hesabatlar 0 kimi yox, "naməlum" sayır |
| **Qaimə-faktura** | Satış üçün A5 PDF sənəd, № formatı `SF-yyyyMMdd-XXXXXX` (Exports modulu) |
| **Bağlanış (Closing)** | Gün sonu kassa üzləşdirməsi. `ExpectedCash = OpeningCash + CashSales − Expenses`; `Difference = ActualCash − ExpectedCash` |
| **Dondurulmuş mal (frozen stock)** | 30/60/90 gün satılmayan məhsullar (Reports) |
| **Sahibkar (sahib)** | Mağaza sahibi rolu — tam səlahiyyət (wire: `role = "sahib"`) |
| **Satıcı (satici)** | Satış edən işçi rolu — məhdud səlahiyyət (wire: `"satici"`) |
| **Menecer** | Wire formatda mövcud rol dəyəri (`"menecer"`) |
| **Xərc növü (ExpenseType)** | İdarə olunan pick-list (Category kimi, ayrı cədvəl, unique ad): Yol pulu, Fəhlə pulu, Yer/Anbar xərci, Paket/Qutu, Gömrük, Mağaza xərci, Digər (seed). `Expense.Category` bunun sərbəst-string snapshot-udur — növ silinsə/adı dəyişsə köhnə xərclər pozulmur |
| **Xərc mənbəyi (Source)** | `Expense.Source`: `"product"` (mala bağlı, ProductId dolu, real mayaya təsir edir) və ya `"general"` (ümumi mağaza xərci, ProductId yoxdur, mayaya təsirsiz) |
| **WhatsApp şablonu** | Settings-də borc xatırlatma mesajı şablonu; `{debt}` placeholder-ini frontend əvəz edir. Backend mesaj göndərmir |
| **Açıq faktura linki** | Satışın sabit tokenli auth-suz PDF linki (`/api/public/invoices/{token}`) — WhatsApp-la paylaşmaq üçün; IP başına 30/dəq limit |

## Last Updated

2026-07-27 — BE#4: «Xərc növü (ExpenseType)» və «Xərc mənbəyi (Source)» terminləri əlavə olundu; köhnə sabit «Xərc kateqoriyaları» sətri çıxarıldı (artıq idarə olunan növlərdir, wire-da frozen deyil).

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/WireFormat.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Products/Domain/Product.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Domain/Sale.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.DayEnd/Domain/Closing.cs`
- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Domain/ExpenseType.cs`, `ExpenseSource.cs`
