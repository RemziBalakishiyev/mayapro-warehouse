# API Overview

Bütün route-lar `/api/...`, JSON camelCase, tarixlər ISO 8601, pul decimal (JSON number). Wire dəyərləri (ödəniş növləri, rollar) dondurulub — bax [ADR-0006](../decisions/0006-frozen-wire-format.md). Xəta formatı: `docs/api/ERROR-CONTRACT.md`.

**Auth səviyyələri:** `anon` = açıq · `auth` = istənilən login olmuş rol · `O+M` = OwnerOrManager policy · `O` = OwnerOnly policy. Rol çatmır → 403.

## Endpoint-lər (48)

### Auth (`/api/auth`, `/api/employees`)
| Verb | Route | Auth | Qeyd |
|---|---|---|---|
| POST | `/api/auth/login` | anon | `{phone, password}` → `{token, user}` |
| GET | `/api/auth/me` | auth | Cari istifadəçi |
| GET | `/api/employees` | auth | İşçi siyahısı |

### Products (`/api/products`, `/api/categories`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/products` · `/api/products/{id}` | auth |
| POST | `/api/products` | O+M |
| PUT / DELETE | `/api/products/{id}` | O+M |
| POST | `/api/products/{id}/adjust-stock` (`{delta, note}`) | auth |
| POST | `/api/products/{id}/generate-barcode` | O+M |
| GET / POST | `/api/categories` | auth |

`POST /api/products/{id}/generate-barcode` barkodu boş olan mala `SDK` + 7 rəqəm formatında unikal barkod verir və yenilənmiş `ProductDto`-nu qaytarır. Barkodu artıq varsa → 409 `Products.BarcodeAlreadyExists` «Malın artıq barkodu var» (təkrar generasiya yoxdur). Unikallığı `Barcode` üzərindəki filtrli unique index təmin edir; toqquşmada handler yeni namizədlə save-i təkrarlayır (maks. 5 cəhd).

### Sales (`/api/sales`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/sales?date&from&to&take&skip` (PagedResult) | auth |
| GET | `/api/sales/{id}` (detal + müştəri adı + cari məhsul adı) | auth |
| POST | `/api/sales` | auth |
| POST | `/api/sales/{id}/invoice-link` → `{url}` (token ilk çağırışda yaranır, sonra sabit) | auth |
| PUT / DELETE | `/api/sales/{id}` | O+M |

POST/PUT `/api/sales` optional `paidAmount` (nullable decimal) və `paidVia` (`"Nağd"`\|`"Kart"`, default `"Nağd"`) qəbul edir (BE#15). `paidAmount` göndərilmirsə nağd/kartda yekun, nisyədə 0 sayılır (geriyə uyğunluq — köhnə body-lər dəyişmədən işləyir). Qaydalar: `0 ≤ paidAmount ≤ salePrice × quantity`; qalıq (`totalAmount − paidAmount`) > 0 olanda `customerId` MƏCBURİdir (400 «Qalıq borc üçün müştəri seçilməlidir») və satış Nisyə kimi saxlanılır — göndərilən `paymentType` nə olursa olsun; müştəri borcu YALNIZ qalıq qədər artır. Digər 400-lar: «Ödənilən məbləğ mənfi ola bilməz», «Ödənilən məbləğ ümumi məbləğdən çox ola bilməz», «Ödəniş üsulu Nağd və ya Kart olmalıdır». Cavab DTO-larında (`SaleDto`, `SaleDetailDto`) `paidAmount`, `remainingAmount` (hesablanmış) və `paidVia` sahələri var. Qaimə PDF-i qalıq varsa «Ödənildi: X · Qalıq borc: Y» sətrini göstərir (məbləğlər invoice-un qalan hissəsi kimi `N2` + mağazanın valyutası ilə).

POST/PUT `/api/sales` optional `purchasePricePerUnit` (nullable decimal) qəbul edir — yalnız sərbəst satışda oxunur (kataloq satışında məhsulun `PurchasePrice`-ı snapshot olunur, göndərilən dəyər nəzərə alınmır). Mənfi → 400 «Alış qiyməti mənfi ola bilməz». Cavab DTO-larında (`SaleDto`, `SaleDetailDto`) `purchasePricePerUnit` sahəsi var; açıq faktura PDF-i bu sahəni GÖSTƏRMİR (`SaleInvoiceInfo`-da maya/alış sahələri yoxdur).

### Customers (`/api/customers`)
| Verb | Route | Auth |
|---|---|---|
| GET / POST | `/api/customers` | auth |
| GET | `/api/customers/open-debts` | auth |
| GET | `/api/customers/{id}/payments` · `/{id}/history` | auth |
| POST | `/api/customers/{id}/payments` (`{amount, note}`) | auth |
| PUT | `/api/customers/{id}` | O+M |
| DELETE | `/api/customers/{id}/credits/{saleId}` | O+M |
| DELETE | `/api/customers/{id}` | O |

`GET /api/customers/open-debts` (BE#21) — bütün müştərilərin hələ bağlanmamış borc mənbələri: `{items[], totalRemaining}`. Hər sətir: `customerId`, `customerName`, `phone`, `source` (`"sale"` | `"initialDebt"`), `sourceDate` (UTC), `description` (satışda `«mal adı × say»`, ilkin borcda `«İlkin borc»`), `originalAmount` (satışda borc yaradan QALIQ, ilkin borcda məbləğ), `paidSoFar`, `remaining`, `daysOld` (Asia/Baku tam günləri). Ödənişlər FIFO — ən köhnə mənbədən başlayaraq — silinir; tam ödənilmiş mənbə siyahıya DÜŞMÜR. Sıralama: ən köhnə borc əvvəldə. Hesablama sorğu anında aparılır (ayrıca cədvəl yoxdur, dörd sorğu: müştərilər + ilkin borclar + qruplaşdırılmış ödəniş cəmləri + `ISalesModule.GetOutstandingSalesAsync`). Bir müştərinin `remaining` cəmi onun `Debt` sahəsi ilə üst-üstə düşməlidir — düşmürsə sorğu uğurla cavab verir, uyğunsuzluq yalnız warning kimi log-a yazılır. Silinmiş müştəriyə aid qalıqlı satış sətirləri siyahıya düşmür.

### Suppliers (`/api/suppliers`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/suppliers` · `/{id}/payments` · `/{id}/history` | auth |
| POST | `/api/suppliers` · `/{id}/debts` · `/{id}/payments` | O+M |
| PUT | `/api/suppliers/{id}` | O+M |
| DELETE | `/api/suppliers/{id}` (borc qalıbsa 409) | O |

POST `/api/suppliers` optional `debt` (ilkin borc, default 0) qəbul edir; mənfi → 400 «Borc mənfi ola bilməz». `debt > 0` olduqda `SupplierDebtAdjustment` tarixçə sətri də yazılır. `GET /{id}/history` = ilkin borc + ödənişlər, xronoloji ARTAN sırada (`{date, type, amount, note}`, `type` = `initialDebt` | `payment`). Köhnə `GET /{id}/payments` dəyişməz qalıb — YALNIZ ödənişləri, tarix üzrə AZALAN sırada qaytarır.

### Expenses (`/api/expenses`, `/api/expense-types`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/expenses?month&source` | auth |
| POST | `/api/expenses` | O+M |
| PUT / DELETE | `/api/expenses/{id}` | O+M |
| GET / POST | `/api/expense-types` | auth |

`source` (idarə olunan xərc mənbəyi: `general` \| `product`) POST/PUT `/api/expenses`-də MƏCBURİdir və `productId` ilə uyğun olmalıdır (`product` → productId var, `general` → yoxdur); uyğunsuzluq/naməlum dəyər 400. `GET /api/expenses` üzərindəki `source` filtri optionaldır, naməlum dəyər 400 (`Expenses.InvalidSource`). `category` artıq sabit kod (EXP_CATS) deyil — idarə olunan `ExpenseType`-ın sərbəst-string ad snapshot-udur (dublikat ad → 400 `Expenses.ExpenseTypeDuplicate`). `GET /api/reports/summary` cavabına `generalExpenses`/`productExpenses` bölgüsü əlavə olundu (cəmi `expenses` sahəsinə bərabərdir).

### DayEnd (`/api/closings`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/closings` · `/api/closings/today` | auth |
| POST | `/api/closings` (`{openingCash, actualCash, note}`) | O |

### Reports (`/api/reports`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/reports/dashboard` | auth |
| GET | `/api/reports/summary?period=today\|week\|month\|all` | auth |

### Settings (`/api/settings`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/settings` | auth |
| PUT | `/api/settings` | O |

### Exports (`/api/exports`) — hamısı auth
`GET /products.xlsx` · `GET /sales.pdf?from&to` · `GET /sales/{id}/invoice.pdf` · `POST /products/labels.pdf`

`POST /api/exports/products/labels.pdf` — barkod/QR etiket vərəqi. Body: `{ items: [{ productId, count }], type?: "barcode" | "qr" }` (default `barcode`). A4-də 3×8 grid (63×34mm etiket, 2mm kəsim boşluğu), hər etiketdə mal adı (maks. 2 sətir), qalın satış qiyməti (`12.50 ₼`, invariant format), kod şəkli və altında barkod mətni. `Content-Disposition: attachment; filename="etiketler-{yyyy-MM-dd}.pdf"`.

400 halları: `Exports.NoLabelItems` (boş body / `items` boş və ya null element) · `Exports.InvalidLabelCount` (`count <= 0`) · `Exports.TooManyLabels` (cəmi > 500) · `Exports.UnknownProducts` (tapılmayan id-lər) · `Exports.ProductsWithoutBarcode` (barkodsuz malların adları ilə). Eyni `productId` bir neçə dəfə göndərilə bilər — hər sətir öz nüsxələrini verir, cəmi yenə 500 limitinə tabedir.

### Public (`/api/public`) — AUTH-SUZ
`GET /api/public/invoices/{token}` — token ilə qaimə PDF, inline (WhatsApp paylaşımı). Rate limit: IP başına 30/dəq (429). Yanlış token → 404.

### Activity, Health
`GET /api/activity?take&skip` (auth) · `GET /health` (anon)

## DTO referansı

Dəqiq DTO sahələri üçün: modulun `Application/Contracts/*Dto.cs` faylları; frontend tipləri `docs/index.ts` (kontraktın frontend tərəfi); test wire assert-ləri `tests/.../WireFormatApiTests.cs`.

## Last Updated

2026-07-30 — BE#15: POST/PUT `/api/sales` üzərinə `paidAmount`/`paidVia`, cavab DTO-larına `paidAmount`/`remainingAmount`/`paidVia`; «Nisyə satış üçün müştəri seçilməlidir» mesajı «Qalıq borc üçün müştəri seçilməlidir» ilə əvəz olundu.

2026-07-30 — BE#12: `POST /api/products/{id}/generate-barcode` (O+M, SDK barkodu) və `POST /api/exports/products/labels.pdf` (barkod/QR etiket vərəqi).

2026-07-27 — BE#4: `GET/POST /api/expense-types` (idarə olunan xərc növləri), `Expense.category` sərbəst string oldu, `source` (general/product) sahəsi + `GET /api/expenses?source` filtri, summary-ə `generalExpenses`/`productExpenses`; təchizatçı ilkin borcu + `GET /api/suppliers/{id}/history`.

## Related Code

- `src/Modules/*/Endpoints/*.cs` (bütün route-lar)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs` (policy tərifləri)
