# Error Contract

Biznes xətaları exception deyil — `Result` / `Result<T>` pattern (`SharedKernel/Application/Result.cs`).

## Wire formatı

Bütün xəta cavablarının body-si eyni formadadır (frontend api-client birbaşa toast göstərir):

```json
{ "code": "Sales.InsufficientStock", "message": "Stokda kifayət qədər mal yoxdur" }
```

- `code` — sabit maşın kodu, `Modul.XetaAdi` formatında
- `message` — istifadəçiyə göstərilən mesaj, **həmişə Azərbaycanca**

## HTTP status konvensiyası

Status kodu error code-un **suffiksindən** avtomatik seçilir (`ResultExtensions.StatusCodeFor`) — modullar HTTP-dən xəbərsizdir:

| Code suffiksi | Status |
|---|---|
| `...NotFound` | 404 |
| `...Conflict`, `...AlreadyExists`, `...AlreadyClosed` | 409 |
| qalan hamısı | 400 |

Uğur: `ToHttpResult()` → 200; `ToCreatedResult(location)` → 201 + Location header.

**Yeni error yazanda:** 404 istəyirsənsə kodu mütləq `NotFound` ilə bitir (məs. `Exports.SaleNotFound`).

## Validation

- FluentValidation; handler yalnız **ilk** xətanı qaytarır: `Error.Validation(validation.Errors[0].ErrorMessage)` → code `General.Validation`, status 400.
- Generic helper-lər: `Error.NotFound(msg)` → `General.NotFound`, `Error.Conflict(msg)` → `General.Conflict`.

## Auth xətaları

- Token yoxdur/yanlışdır → 401 (framework, body-siz)
- Rol icazəsi çatmır (məs. OwnerOnly) → 403 (framework, body-siz)

## Gözlənilməz xətalar

`GlobalExceptionHandler` (`src/MayaPro.WarehouseApi.Api/Middleware/`) → 500 + generic Azərbaycanca mesaj; detallar Serilog-a.

## Modul error kataloqları

Hər modulun `Domain/<Modul>Errors.cs` faylı var (məs. `SaleErrors`, `CustomerErrors`, `ProductErrors`) — mövcud kodların siyahısı üçün həmin fayllara bax.

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Application/ResultExtensions.cs` (status mapping)
- `src/MayaPro.WarehouseApi.SharedKernel/Application/Error.cs`, `Result.cs`
- `src/MayaPro.WarehouseApi.Api/Middleware/GlobalExceptionHandler.cs`
- `src/Modules/*/Domain/*Errors.cs`
