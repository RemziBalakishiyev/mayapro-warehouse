# Changelog

Əhəmiyyətli dəyişikliklərin qısa qeydiyyatı — yeni girişlər yuxarıya. Tam tarixçə üçün `git log`.

## 2026-07-25

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

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `git log` (tam tarixçə)
