# Export Flow — Excel / PDF / Faktura

Exports modulunun cədvəli yoxdur — hər şeyi kontraktlardan oxuyur. Hamısı `auth` (satıcı daxil). Azərbaycanca hərflər üçün embed Noto Sans şriftləri (`ExportFonts.EnsureRegistered`, idempotent). QuestPDF Community lisenziyası `ExportsModule.RegisterServices`-də set olunur.

## `GET /api/exports/products.xlsx`

ClosedXML — məhsul kataloqu. Sətir 1: mağaza adı + tarix; sətir 2: başlıqlar; 3+: məhsullar. Fayl: `mallar-*.xlsx`.

## `GET /api/exports/sales.pdf?from&to`

QuestPDF A4 dövr hesabatı. Default dövr: cari ayın 1-i → bu gün. `from > to` → `Exports.InvalidRange`; yanlış tarix formatı → 400 `Exports.InvalidFrom/InvalidTo`. Xülasə bloku (satış sayı, cəm, qazanc + naməlum qazanc sayı, xərclər, nağd/kart/nisyə bölgüsü) + satır-satır cədvəl (sərbəst satış `*` ilə). Fayl: `satislar-{from}-{to}.pdf`.

## `GET /api/exports/sales/{id}/invoice.pdf` — qaimə-faktura

A5 format. Satış yoxdursa `Exports.SaleNotFound` (404).

- № formatı: `SF-{Bakı tarixi yyyyMMdd}-{saleId ilk 6 hex, uppercase}`; fayl `faktura-{№}.pdf`.
- Başlıq: mağaza adı + ünvan/telefon (Settings-dən, yalnız doludursa) | "QAİMƏ-FAKTURA" + № + Bakı tarix-saatı.
- Müştəri bloku: nisyədə ad + telefon (`ICustomersModule.GetCustomerInfoAsync`; müştəri silinibsə "—"); nağdda "Nağd satış", kartda "Kartla ödəniş".
- Cədvəl çoxsətirli struktura hazırdır (hazırda hər satış bir mal).
- Cəm + YEKUN (valyuta Settings-dən); nisyədə əlavə: "Ödəniş: Nisyə" + "Ümumi qalıq borc: X" (müştərinin CARİ borcu).

## Açıq faktura linki (WhatsApp paylaşımı)

1. `POST /api/sales/{id}/invoice-link` (auth, Sales modulu) → `{url}`. Token İLK çağırışda yaranır (32 kriptoqrafik random bayt → Base64Url, 43 simvol) və `Sale.InvoiceToken`-da saxlanır; sonrakı çağırışlar EYNİ linki qaytarır. URL bazası: `App:PublicBaseUrl` (dev: `http://localhost:5208`).
2. `GET /api/public/invoices/{token}` — **auth-suz** (modulda yeganə anonim səth; login pattern-i kimi `AllowAnonymous`). Token Sales kontraktı ilə satışa çevrilir (`GetSaleIdByInvoiceTokenAsync`), mövcud faktura generatoru çağırılır, PDF **inline** qaytarılır (telefonda brauzerdə açılır). Hər cür uğursuzluq eyni 404-ə yığılır (`Exports.InvoiceNotFound`) — token mövcudluğu sızdırılmır.
3. Qoruma: IP başına **30 sorğu/dəq** rate limit (host-da `PublicInvoice` policy, aşımda 429).

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Exports/Application/UseCases/`
- `src/Modules/MayaPro.WarehouseApi.Modules.Exports/Application/ExportFonts.cs`
