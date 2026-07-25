# API Overview

Bütün route-lar `/api/...`, JSON camelCase, tarixlər ISO 8601, pul decimal (JSON number). Wire dəyərləri (ödəniş növləri, rollar) dondurulub — bax [ADR-0006](../decisions/0006-frozen-wire-format.md). Xəta formatı: `docs/api/ERROR-CONTRACT.md`.

**Auth səviyyələri:** `anon` = açıq · `auth` = istənilən login olmuş rol · `O+M` = OwnerOrManager policy · `O` = OwnerOnly policy. Rol çatmır → 403.

## Endpoint-lər (40)

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
| PUT / DELETE | `/api/sales/{id}` | O+M |

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
| GET | `/api/suppliers` · `/{id}/payments` | auth |
| POST | `/api/suppliers` · `/{id}/debts` · `/{id}/payments` | O+M |
| PUT | `/api/suppliers/{id}` | O+M |
| DELETE | `/api/suppliers/{id}` (borc qalıbsa 409) | O |

### Expenses (`/api/expenses`)
| Verb | Route | Auth |
|---|---|---|
| GET | `/api/expenses?month` | auth |
| POST | `/api/expenses` | O+M |
| PUT / DELETE | `/api/expenses/{id}` | O+M |

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

### Activity, Health
`GET /api/activity?take&skip` (auth) · `GET /health` (anon)

## DTO referansı

Dəqiq DTO sahələri üçün: modulun `Application/Contracts/*Dto.cs` faylları; frontend tipləri `docs/index.ts` (kontraktın frontend tərəfi); test wire assert-ləri `tests/.../WireFormatApiTests.cs`.

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/*/Endpoints/*.cs` (bütün route-lar)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs` (policy tərifləri)
