# Changelog

Əhəmiyyətli dəyişikliklərin qısa qeydiyyatı — yeni girişlər yuxarıya. Tam tarixçə üçün `git log`.

## 2026-07-27

- **Xərc tarixi gələcək ola bilməz** (BE#9): `CreateExpenseValidator` və `UpdateExpenseValidator` `IDateProvider` alır və `date` göndərilibsə `ToLocalDate(date) <= Today` (Asia/Baku, ADR-0005) yoxlayır → 400 «Xərcin tarixi gələcək ola bilməz». `date = null` (yaratmada "indi", düzəlişdə köhnə tarix) toxunulmayıb. FE#10-dakı yalnız-UI qorumasının backend qarşılığı; hesabatlardakı "Bu ay" fərqinin kökü bağlanır. `CreateExpenseHandler` default tarixi artıq `DateTime.UtcNow` yox, `IDateProvider.UtcNow` ilə yazır (eyni saat mənbəyi). Migration/kontrakt dəyişikliyi yoxdur.
- **İdarə olunan xərc növləri + xərc mənbəyi ayrımı** (BE#4): yeni `ExpenseType` (Category-nin analoqu — unique ad, `GET/POST /api/expense-types`, hər rola açıq, seed Development-də 7 default növ). `Expense.Category` enum-dan sərbəst-string snapshot-a keçdi (`nvarchar(20)`→`nvarchar(100)`); köhnə `ExpenseCategory`/`ExpenseCategoryCode` tamamilə çıxarıldı. Yeni `Expense.Source` (daxildə enum, wire `"general"`\|`"product"`): `product` — `ProductId` məcburi, maya zənciri işə düşür; `general` — `ProductId` qadağan, mayaya təsirsiz. `CreateExpenseValidator`/`UpdateExpenseValidator`-da qarşılıqlı validasiya. `GET /api/expenses` üzərinə optional `source` filtri (mövcud `month` ilə birgə), naməlum dəyər → 400 (`Expenses.InvalidSource`). `GetSummaryHandler`/`SummaryDto`-ya `generalExpenses`/`productExpenses` bölgüsü (cəmi mövcud `expenses`-ə bərabər, `netProfit` dəyişməyib) — kontrakt: `ExpenseReportRow`-a `Source` sahəsi. Migration `ExpenseTypesAndSource`: köhnə enum dəyərləri Azərbaycanca adlara çevrilir (Transport→Yol pulu və s.), `Source` `ProductId`-dən backfill olunur (dolu→product, boş→general). **Breaking wire change**: `POST/PUT /api/expenses` indi `source` sahəsini MƏCBURİ tələb edir; `category` artıq sabit EXP_CATS kodu deyil, sərbəst string. Review düzəlişləri: dublikat növ adı yoxlaması açıq şəkildə case-insensitive (DB collation-undan asılı deyil), ad/kateqoriya üçün 100 simvol validasiyası (DB truncation → 500 əvəzinə 400), migration-da `Source` default constraint olmadan nullable→backfill→NOT NULL ardıcıllığı ilə doldurulur və `Down()` naməlum adları `Other`-ə yığır.
- **Təchizatçı ilkin borcu + tarixçə** (BE#3): yeni `SupplierDebtAdjustment` entity (`suppliers.SupplierDebtAdjustments`, migration `AddSupplierDebtAdjustments`, `SupplierId` üzərində index) — müştəri tərəfindəki `CustomerDebtAdjustment` pattern-inin güzgüsü. `POST /api/suppliers` `debt > 0` göndəriləndə təchizatçı + ilkin borc sətri + `"{ad} — ilkin borc {məbləğ} AZN"` activity log-u TƏK `IUnitOfWork` transaction-ında yazılır; `debt = 0`-da köhnə davranış olduğu kimi qalır, mənfi `debt` yenə 400. Yeni `GET /api/suppliers/{id}/history` — ilkin borc (`initialDebt`) + ödənişlər (`payment`) xronoloji artan sırada. Köhnə `GET /api/suppliers/{id}/payments` kontraktı dəyişməyib. Təchizatçı silinəndə ilkin borc sətirləri də təmizlənir (FK cascade yoxdur). Bilinən boşluq: `POST /{id}/debts` (kreditlə alış) hələ tarixçə sətri yaratmır.

## 2026-07-26

- **Satışda maya və alış qiymətinin ayrılması** (BE#1): `Sale.PurchasePricePerUnit` (nullable, migration `AddSalePurchasePricePerUnit`) — kataloq satışında məhsulun `PurchasePrice`-ı snapshot olunur (kontrakta `ProductStockSnapshot.PurchasePrice` əlavə edildi), sərbəst satışda command-dan olduğu kimi yazılır. `CostPerUnit`/`Profit` formulları toxunulmadı. Wire: `purchasePricePerUnit` (POST/PUT `/api/sales` optional giriş; `SaleDto`/`SaleDetailDto` çıxışı), mənfi dəyər 400. Migration mövcud sərbəst satışların alış qiymətini mayadan bərpa edir (xərc payı çıxılır), kataloq satışlarında NULL qalır.

## 2026-07-25

- **Müştəri bütün satış növlərində**: `customerId` artıq nağd/kartda da göndərilə bilər (nisyədə məcburi qalır); borc təsiri yalnız nisyədə. Müştəri statistikaları bütün satışları əhatə edir: `lastPurchaseDate` son istənilən satış, yeni `totalPurchases`/`purchaseCount` sahələri (`GetPurchaseStatsByCustomerAsync` — tək qruplaşdırılmış sorğu). Tarixçəyə nağd/kart satışlar da düşür (`paymentType` sahəsi ilə). Kontrakt: `GetLastCreditSaleDatesByCustomerAsync`→`GetPurchaseStatsByCustomerAsync`, `GetCreditSalesByCustomerAsync`→`GetSalesByCustomerAsync`.
- **Qaimə açıq linki (WhatsApp)**: `Sale.InvoiceToken` (nullable, unique filtered index, migration) — ilk istəkdə kriptoqrafik token yaranır, sonra sabit. `POST /api/sales/{id}/invoice-link` → `{url}` (`App:PublicBaseUrl` bazası). `GET /api/public/invoices/{token}` — auth-suz inline PDF, IP başına 30/dəq rate limit (host `PublicInvoice` policy, `UseRateLimiter`). Yeni kontrakt: `ISalesModule.GetSaleIdByInvoiceTokenAsync`.
- **Project knowledge sistemi**: `docs/` altında strukturlaşdırılmış sənədlər (INDEX router, business/flows/api/architecture/database/decisions/changes), `.claude/rules/documentation.md` workflow, CLAUDE.md optimallaşdırması. Kod dəyişikliyi yoxdur.
- **Satış fakturası PDF** (`8b17b6d`): `GET /api/exports/sales/{id}/invoice.pdf` — A5 qaimə-faktura (QuestPDF). StoreSettings-ə `Address` + `Phone` (nullable) əlavə olundu (migration + DTO + PUT). Yeni kontrakt metodları: `ISalesModule.GetInvoiceSaleAsync`, `ICustomersModule.GetCustomerInfoAsync`, `ISettingsModule.GetStoreInfoAsync`, `IDateProvider.ToLocalDateTime`.
- **Endirim ləğvi + nisyə satışın silinməsi** (`4a9cf08`): `Sale.Discount` sahəsi tamamilə çıxarıldı (migration ilə) — `TotalAmount = Subtotal`. `ISalesModule.DeleteCreditSaleAsync` müştəri borc UI-dan nisyə satışı silir; `CustomerCreditSale`-ə `Id` əlavə olundu.

## 2026-07 (əvvəlki mərhələlər, xronoloji)

- Solution skeleti, SharedKernel (Result/Error, IModule, UnitOfWork), Serilog, Swagger (`2e84912`)
- Auth (Identity) modulu: login, JWT, rollar (`3cb51e0`)
- Products modulu + ilk modullararası kontrakt (`459e161`)
- Sales modulu: satış zənciri tək transaction-da (`b4e6397`)
- Suppliers + Expenses: xərc→maya zənciri (`e561820`)
- Activity + DayEnd: real activity logger, gün bağlanışı (`80eca40`)
- Reports + Settings modulları (`b384248`)
- Kontrakt uyğunluğu düzəlişləri, dashboard tamamlanması, Bakı timezone (`309e696`, `96e0a50`)
- Azərbaycanca identifikatorlar ingilisləşdi, wire format qorundu (`482d9fc`)
- Kateqoriyalar + dinamik məhsul xüsusiyyətləri (JSON attributes) (`3683711`)
- SQL bağlantı dayanıqlılığı, connection scope auditi (`bd90d69`)
- Sərbəst (manual) satış — mal seçmədən əl ilə (`61469b3`)
- Satışda kateqoriya snapshot (`9517411`)
- Satış tarixçəsi pagination + tarix aralığı (`5d46591`)
- Məhsul xərcləri sərbəst adlı sətirlər (JSON) (`43bc4fb`)
- Excel/PDF export modulu (`e27dc9d`)
- Müştəri ilkin borc + tam borc tarixçəsi (`6423278`)
- Satış detalı endpoint + sərbəst satış xərc sətirləri (`116a90a`)
- Delete/update əməliyyatları zəncir geri sarılması ilə (`b2a3724`)

## Last Updated

2026-07-27 — BE#9 (xərc tarixinin gələcək ola bilməməsi), BE#4 (idarə olunan xərc növləri + xərc mənbəyi ayrımı), BE#3 (təchizatçı ilkin borcu + tarixçə endpoint-i).

## Related Code

- `git log` (tam tarixçə)
