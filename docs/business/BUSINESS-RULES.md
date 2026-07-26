# Business Rules

Bazar (Sədərək) anbar-satış sistemi. Domain: məhsul kataloqu + stok, satış (nağd/kart/nisyə), müştəri borcu, təchizatçı borcu, xərclər, gün sonu kassa üzləşdirməsi.

## Rollar və icazələr

Rollar: **Owner** (`sahib`), **Manager** (`menecer`), **Seller** (`satici`). JWT `role` claim-i enum adıdır (Owner/Manager/Seller), wire-da Azərbaycanca kod gedir.

- Seller edə bilər: satış (yaratma), müştəri yaratma/ödəniş qəbulu, stok korreksiyası, kateqoriya əlavəsi, baxış/export.
- Yalnız Owner+Manager: məhsul CRUD, satış düzəlişi/silinməsi, təchizatçı yazıları, xərclər, müştəri düzəlişi, nisyə sətri silmə.
- Yalnız Owner: gün bağlama, settings dəyişmə, müştəri/təchizatçı silmə.

Dəqiq endpoint-icazə cədvəli: `docs/api/API-OVERVIEW.md`.

## Satış qaydaları

- Satış növləri: kataloq satışı (`productId` var) və **sərbəst satış** (`productId = null`, `IsManual`, stok hərəkəti yoxdur).
- `TotalAmount = UnitPrice × Quantity` (endirim sahəsi YOXDUR — ADR-0007).
- `Profit = (UnitPrice − CostPerUnit) × Quantity`; maya bilinməyəndə `null` (0 sayılmır, hesabatlarda ayrıca "naməlum" göstərilir).
- Satış anında ad/kateqoriya/maya snapshot olunur (ADR-0004).
- **Maya ≠ alış qiyməti**: `CostPerUnit` (real maya) ilə yanaşı `PurchasePricePerUnit` (təmiz alış qiyməti) ayrıca saxlanılır. Kataloq satışında məhsulun `PurchasePrice`-ı snapshot olunur; sərbəst satışda command-dan olduğu kimi yazılır (yenidən hesablanmır, mənfi ola bilməz). Qazanc HƏMİŞƏ yalnız `CostPerUnit`-dən hesablanır — alış qiyməti hesablamaya girmir. Bilinmirsə `null` (0 yazılmır).
- Stok satışdan böyükdürsə `Sales.InsufficientStock`; stok heç vaxt 0-dan aşağı düşmür.
- `customerId` HƏR ödəniş növündə göndərilə bilər (nağd/kartda istəyə bağlı, nisyədə MƏCBURİ). Borca təsir yalnız nisyədə: satış cəmi müştəri borcuna əlavə olunur; nağd/kartda müştəri yalnız alış tarixçəsi üçündür.
- Satış düzəlişi = köhnə effektlər geri sarılır + yeni dəyərlərlə yenidən tətbiq (eyni Id/tarix/satıcı qalır). Günü bağlanmış satış redaktə oluna bilməz (`Sales.DayClosedConflict`, 409). Silmədə bağlı gün qoruması YOXDUR.
- Silinmə/düzəliş geri sarılması best-effort-dur: məhsul/müştəri artıq silinibsə zəncir yenə işləyir.

## Stok və maya qaydaları

- `RealCostPerUnit = PurchasePrice + (partiya xərcləri cəmi ÷ InitialQuantity)`, 2 rəqəmə yuvarlanır (AwayFromZero).
- `InitialQuantity` yaradılış anında fiksasiya olunur, ömürlük məxrəcdir.
- Məhsula bağlı xərc yaradılanda `AddExpenseToProductAsync` → maya yenidən hesablanır; xərc silinəndə/düzələndə əks proses.
- `AdjustStock(delta)` — əl korreksiyası, 0-da floor.
- Məhsul silmək təhlükəsizdir — satışlar öz snapshot-larını daşıyır.

## Xərc qaydaları

- **Xərc növləri (ExpenseType)** idarə olunan pick-list-dir (Category-nin analoqu): ayrı cədvəl, unique ad, `GET/POST /api/expense-types` (hər ikisi hər rola açıq). Seed (yalnız Development): Yol pulu, Fəhlə pulu, Yer/Anbar xərci, Paket/Qutu, Gömrük, Mağaza xərci, Digər. `Expense.Category` bu adın sərbəst-string snapshot-udur — FK yoxdur, növ silinsə/adı dəyişsə köhnə xərclər pozulmur.
- **Xərc mənbəyi (Source)**: `"product"` (mala bağlı, `ProductId` MƏCBURİ, `AddExpenseToProductAsync` işə düşür → maya artır) və ya `"general"` (ümumi mağaza xərci, `ProductId` GÖNDƏRİLMƏMƏLİDİR, heç bir malın mayasına toxunmur). Validasiya hər iki istiqamətdə: uyğunsuzluq 400.
- `GET /api/expenses` optional `source=general|product` filtri dəstəkləyir (mövcud `month` filtri ilə birlikdə); naməlum dəyər 400 (`Expenses.InvalidSource`).
- Dashboard/summary xərc CƏMİ (`Expenses`) source-dan asılı olmayaraq bütün xərcləri əhatə edir; `GET /api/reports/summary` üzərinə `generalExpenses`/`productExpenses` bölgüsü əlavə olunub (cəmi `Expenses`-ə bərabərdir), `netProfit` düsturu dəyişməyib.

## Müştəri borcu qaydaları

- Borc yalnız domain metodları ilə dəyişir; heç vaxt 0-dan aşağı düşmür.
- Ödəniş borcdan böyükdürsə imtina: `Customers.PaymentExceedsDebt`.
- Nisyə satış silinəndə borc azalır, amma 0-da floor (borc artıq ödənilibsə mənfiyə düşmür).
- İlkin borc `CustomerDebtAdjustment` sətri kimi tarixçəyə düşür.
- Müştəri tarixçəsi = ilkin borc + BÜTÜN satışlar (hər ödəniş növü, Sales kontraktından; `paymentType` sahəsi fərqləndirir — borcu yalnız Nisyə sətirləri artırıb) + ödənişlər, xronoloji birləşdirilir.
- Müştəri statistikaları bütün satış növlərini əhatə edir: `lastPurchaseDate` son İSTƏNİLƏN satış, `totalPurchases`/`purchaseCount` ömürlük cəm/say (qruplaşdırılmış tək sorğu).
- Müştəri silinəndə ödəniş/ilkin borc tarixçəsi də silinir (borc qalsa belə); köhnə satışlar `CustomerId` saxlayır — frontend "Silinmiş müştəri" göstərir.

## Gün sonu qaydaları

- `ExpectedCash = OpeningCash + CashSales − Expenses`; `Difference = ActualCash − ExpectedCash`. Nisyə satışlar kassa üzləşdirməsinə daxil deyil.
- Satış/xərc totalları HƏMİŞƏ server hesablayır; client yalnız kassa rəqəmlərini göndərir.
- Bir günə bir bağlanış: `DayEnd.AlreadyClosed` (409); real qoruma `Date` üzərində unique index-dir.
- "Bu gün" = Asia/Baku günü (ADR-0005).

## Digər

- Settings singleton sətirdir (sabit Id), ilk oxunuşda default-larla yaranır.
- Təchizatçı borcu qalıbsa silinə bilməz (409).
- WhatsApp: backend mesaj GÖNDƏRMİR — yalnız şablon saxlayır, `{debt}`-i frontend əvəz edir.
- Açıq faktura linki: hər satışın bir dəfə yaranan sabit tokeni var (`Sale.InvoiceToken`); token linki bilən HƏR KƏSƏ auth-suz PDF verir — link paylaşımı müştəri ilə bölüşməyə bərabərdir. IP başına 30/dəq limit.
- Bütün yazma əməliyyatları activity log yazır (siyahı: `src/Modules/*/Application/UseCases/*/`); log caller-in transaction-ında commit olur.

## Last Updated

2026-07-27 — BE#4: idarə olunan xərc növləri (ExpenseType) + xərc mənbəyi ayrımı (Source: general/product).

## Related Code

- `src/Modules/*/Domain/` (entity davranışları + *Errors.cs)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/` (zəncirlər)
- `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/` (ExpenseType, ExpenseSource)
- `docs/handlers.ts` (frontend mock — davranış referansı)
