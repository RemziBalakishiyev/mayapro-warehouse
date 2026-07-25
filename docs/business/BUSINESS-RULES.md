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
- Stok satışdan böyükdürsə `Sales.InsufficientStock`; stok heç vaxt 0-dan aşağı düşmür.
- Nisyə satışda `customerId` mütləqdir; satış cəmi müştəri borcuna əlavə olunur.
- Satış düzəlişi = köhnə effektlər geri sarılır + yeni dəyərlərlə yenidən tətbiq (eyni Id/tarix/satıcı qalır). Günü bağlanmış satış redaktə oluna bilməz (`Sales.DayClosedConflict`, 409). Silmədə bağlı gün qoruması YOXDUR.
- Silinmə/düzəliş geri sarılması best-effort-dur: məhsul/müştəri artıq silinibsə zəncir yenə işləyir.

## Stok və maya qaydaları

- `RealCostPerUnit = PurchasePrice + (partiya xərcləri cəmi ÷ InitialQuantity)`, 2 rəqəmə yuvarlanır (AwayFromZero).
- `InitialQuantity` yaradılış anında fiksasiya olunur, ömürlük məxrəcdir.
- Məhsula bağlı xərc yaradılanda `AddExpenseToProductAsync` → maya yenidən hesablanır; xərc silinəndə/düzələndə əks proses.
- `AdjustStock(delta)` — əl korreksiyası, 0-da floor.
- Məhsul silmək təhlükəsizdir — satışlar öz snapshot-larını daşıyır.

## Müştəri borcu qaydaları

- Borc yalnız domain metodları ilə dəyişir; heç vaxt 0-dan aşağı düşmür.
- Ödəniş borcdan böyükdürsə imtina: `Customers.PaymentExceedsDebt`.
- Nisyə satış silinəndə borc azalır, amma 0-da floor (borc artıq ödənilibsə mənfiyə düşmür).
- İlkin borc `CustomerDebtAdjustment` sətri kimi tarixçəyə düşür.
- Borc tarixçəsi = ilkin borc + nisyə satışlar (Sales kontraktından) + ödənişlər, xronoloji birləşdirilir.
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
- Bütün yazma əməliyyatları activity log yazır (siyahı: `src/Modules/*/Application/UseCases/*/`); log caller-in transaction-ında commit olur.

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/*/Domain/` (entity davranışları + *Errors.cs)
- `src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/UseCases/` (zəncirlər)
- `docs/handlers.ts` (frontend mock — davranış referansı)
