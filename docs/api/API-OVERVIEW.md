# API Overview

Bütün route-lar `/api/...`, JSON camelCase, tarixlər ISO 8601, pul decimal (JSON number). Wire dəyərləri (ödəniş növləri, rollar) dondurulub — bax [ADR-0006](../decisions/0006-frozen-wire-format.md). Xəta formatı: `docs/api/ERROR-CONTRACT.md`.

**Auth səviyyələri:** `anon` = açıq · `auth` = istənilən login olmuş rol · `O+M` = OwnerOrManager policy · `O` = OwnerOnly policy. Rol çatmır → 403.

<<<<<<< HEAD
## Endpoint-lər (44)
=======
## Endpoint-lər (43)
>>>>>>> origin/main

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
| GET / POST | `/api/categories` | auth |

### Sales (`/api/sales`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/sales?date&from&to&take&skip` (PagedResult) | auth |
| GET | `/api/sales/{id}` (detal + müştəri adı + cari məhsul adı) | auth |
| POST | `/api/sales` | auth |
| POST | `/api/sales/{id}/invoice-link` → `{url}` (token ilk çağırışda yaranır, sonra sabit) | auth |
| PUT / DELETE | `/api/sales/{id}` | O+M |

POST/PUT `/api/sales` optional `purchasePricePerUnit` (nullable decimal) qəbul edir — yalnız sərbəst satışda oxunur (kataloq satışında məhsulun `PurchasePrice`-ı snapshot olunur, göndərilən dəyər nəzərə alınmır). Mənfi → 400 «Alış qiyməti mənfi ola bilməz». Cavab DTO-larında (`SaleDto`, `SaleDetailDto`) `purchasePricePerUnit` sahəsi var; açıq faktura PDF-i bu sahəni GÖSTƏRMİR (`SaleInvoiceInfo`-da maya/alış sahələri yoxdur).

### Customers (`/api/customers`)
| Verb | Route | Auth |
|---|---|---|
| GET / POST | `/api/customers` | auth |
| GET | `/api/customers/{id}/payments` · `/{id}/history` | auth |
| POST | `/api/customers/{id}/payments` (`{amount, note}`) | auth |
| PUT | `/api/customers/{id}` | O+M |
| DELETE | `/api/customers/{id}/credits/{saleId}` | O+M |
| DELETE | `/api/customers/{id}` | O |

### Suppliers (`/api/suppliers`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/suppliers` · `/{id}/payments` · `/{id}/history` | auth |
| POST | `/api/suppliers` · `/{id}/debts` · `/{id}/payments` | O+M |
| PUT | `/api/suppliers/{id}` | O+M |
| DELETE | `/api/suppliers/{id}` (borc qalıbsa 409) | O |

<<<<<<< HEAD
### Expenses (`/api/expenses`, `/api/expense-types`)
=======
POST `/api/suppliers` optional `debt` (ilkin borc, default 0) qəbul edir; mənfi → 400 «Borc mənfi ola bilməz». `debt > 0` olduqda `SupplierDebtAdjustment` tarixçə sətri də yazılır. `GET /{id}/history` = ilkin borc + ödənişlər, xronoloji ARTAN sırada (`{date, type, amount, note}`, `type` = `initialDebt` | `payment`). Köhnə `GET /{id}/payments` dəyişməz qalıb — YALNIZ ödənişləri, tarix üzrə AZALAN sırada qaytarır.

### Expenses (`/api/expenses`)
>>>>>>> origin/main
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
`GET /products.xlsx` · `GET /sales.pdf?from&to` · `GET /sales/{id}/invoice.pdf`

### Public (`/api/public`) — AUTH-SUZ
`GET /api/public/invoices/{token}` — token ilə qaimə PDF, inline (WhatsApp paylaşımı). Rate limit: IP başına 30/dəq (429). Yanlış token → 404.

### Activity, Health
`GET /api/activity?take&skip` (auth) · `GET /health` (anon)

## DTO referansı

Dəqiq DTO sahələri üçün: modulun `Application/Contracts/*Dto.cs` faylları; frontend tipləri `docs/index.ts` (kontraktın frontend tərəfi); test wire assert-ləri `tests/.../WireFormatApiTests.cs`.

## Last Updated

<<<<<<< HEAD
2026-07-27 — BE#4: `GET/POST /api/expense-types` (idarə olunan xərc növləri), `Expense.category` sərbəst string oldu, `source` (general/product) sahəsi + `GET /api/expenses?source` filtri, summary-ə `generalExpenses`/`productExpenses`.
=======
2026-07-27 — təchizatçı ilkin borcu + `GET /api/suppliers/{id}/history`.
>>>>>>> origin/main

## Related Code

- `src/Modules/*/Endpoints/*.cs` (bütün route-lar)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs` (policy tərifləri)
