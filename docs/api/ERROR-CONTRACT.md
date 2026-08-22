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
| `...TokenExpired`, `...TokenNotFound` | 410 |
| `...NotFound` | 404 |
| `...Forbidden` | 403 |
| `...Conflict`, `...AlreadyExists`, `...AlreadyClosed` | 409 |
| qalan hamısı | 400 |

**Konvensiyanın yeganə istisnası (BE#41):** `SubscriptionExpired` — modul prefiksi və `...Forbidden` suffiksi YOXDUR, çünki bu, hər hansı modulun biznes xətası deyil, infrastruktur səviyyəli middleware cavabıdır (`TenantGateMiddleware`); sətir spesifikasiyada dondurulub və frontend onu hardcode edir. Suffiksi olmadığı üçün 403-ü `ResultExtensions.StatusCodeFor`-da **adı ilə** xüsusi olaraq xəritələnir; dəyər `WireFormat.ErrorCodes.SubscriptionExpired`-dədir. Yeni error yazanda bu istisnanı NÜMUNƏ GÖTÜRMƏ — suffiks qaydasına tabe ol.

Uğur: `ToHttpResult()` → 200; `ToCreatedResult(location)` → 201 + Location header.

**Yeni error yazanda:** 404 istəyirsənsə kodu mütləq `NotFound` ilə bitir (məs. `Exports.SaleNotFound`).

## Validation

- FluentValidation; handler yalnız **ilk** xətanı qaytarır: `Error.Validation(validation.Errors[0].ErrorMessage)` → code `General.Validation`, status 400.
- Generic helper-lər: `Error.NotFound(msg)` → `General.NotFound`, `Error.Conflict(msg)` → `General.Conflict`.

## Auth xətaları

- Token yoxdur/yanlışdır → 401 (framework, body-siz)
- Rol icazəsi çatmır (məs. OwnerOnly) → 403 (framework, body-siz)
- **BE#35/BE#36 — tenant qapısı** (`TenantGateMiddleware`, adi `{code, message}` body ilə). Login (`POST /api/auth/login`) EYNİ kod/mesaj cütlərini qaytarır — qapı token verildikdən sonra da işləyir:

  | Hal | Status | Code | Mesaj |
  |---|---|---|---|
  | token-də `tenantId` claim-i yoxdur | 401 | `Auth.TenantMissing` | «Token mağaza məlumatı daşımır — yenidən daxil olun» |
  | mağaza tapılmır | 403 | `Auth.TenantInactiveForbidden` | «Mağaza aktiv deyil» |
  | mağaza təsdiq gözləyir | 403 | `Auth.TenantPendingApprovalForbidden` | «Hesabınız təsdiq gözləyir» |
  | mağaza bloklanıb | 403 | `Auth.TenantBlockedForbidden` | «Abunəliyiniz bitib — əlaqə: {admin telefonu}» |
  | abunə müddəti keçib (status hələ `Active`) | 403 | `SubscriptionExpired` | «Abunəliyiniz bitib — əlaqə: {admin telefonu}» |

  Admin telefonu `PlatformAdmin:Phone` konfiqurasiyasındandır; təyin olunmayıbsa mesaj «Abunəliyiniz bitib — dəstək ilə əlaqə saxlayın» olur. `PlatformAdmin` rolu ilə gələn token qapıdan keçir (heç bir mağazaya aid deyil). Detallar: [`multi-tenancy.md`](../multi-tenancy.md)
- **BE#36 — Tenancy**: `Tenancy.PhoneAlreadyExists` → 409 («Bu telefon nömrəsi artıq qeydiyyatdadır», qeydiyyat qlobal telefon yoxlaması; BE#46-dan sonra müqayisə **kanonik** dəyər üzərindədir — `+994 (50) 123-45-67` mövcud `994501234567`-ni tapır) · `Tenancy.TenantNotFound` → 404 («Mağaza tapılmadı»)
- **BE#46 — telefon formatı**: `General.Validation` → **400**, mesaj hərfən «Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)». Telefon qəbul edən HƏR endpoint-dən gələ bilər: `POST`/`PUT /api/customers`, `POST`/`PUT /api/suppliers`, `PUT /api/settings`, `POST /api/auth/register`, `POST /api/admin/tenants`. Qəbul edilən yazılışlar: `994XXXXXXXXX`, `0XXXXXXXXX` və bunların boşluq/`+`/`-`/`(`/`)`/`.` ilə yazılmış variantları; **9 rəqəmli `501234567` qəsdən rədd olunur**. Boş optional telefon xəta DEYİL (`NULL` yazılır); boş məcburi telefon köhnə «Telefon boş ola bilməz» mesajını saxlayır.
  **`POST /api/auth/login` istisnadır**: oxuna bilməyən nömrə format mesajını YOX, mövcud neytral `Auth.InvalidCredentials` → «Telefon və ya şifrə yanlışdır» cavabını alır — iki fərqli mesaj «bu nömrə mövcuddur/yoxdur» siqnalı olardı. Boş telefon yenə 400 «Telefon boş ola bilməz».

## Gözlənilməz xətalar

`GlobalExceptionHandler` (`src/MayaPro.WarehouseApi.Api/Middleware/`) → 500 + generic Azərbaycanca mesaj; detallar Serilog-a.

## Modul error kataloqları

Hər modulun `Domain/<Modul>Errors.cs` faylı var (məs. `SaleErrors`, `CustomerErrors`, `ProductErrors`) — mövcud kodların siyahısı üçün həmin fayllara bax.

## Last Updated

2026-08-22 — BE#46: telefon formatı xətası (`General.Validation`, 400, «Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)») bütün telefon qəbul edən endpoint-lərə əlavə olundu; login-də format xətası neytral `Auth.InvalidCredentials` ilə örtülür; `Tenancy.PhoneAlreadyExists` müqayisəsi kanonik dəyər üzərindədir.

2026-08-16 — BE#41: abunə müddəti keçmiş mağazanın kodu `Auth.SubscriptionExpiredForbidden` → **`SubscriptionExpired`** (spesifikasiyada dondurulmuş sətir); suffiks konvensiyasından qəsdən kənarda qalan yeganə kod, 403-ü adı ilə xəritələnir.

2026-08-16 — BE#36: tenant qapısının 403-ləri statusa görə ayrıldı (`TenantPendingApproval` / `TenantBlocked` / `SubscriptionExpired`); `Tenancy.PhoneAlreadyExists` (409), `Tenancy.TenantNotFound` (404).
2026-08-16 — BE#35: `...Forbidden` → 403 suffiksi, tenant qapısı xətaları, `Products.BarcodeDuplicate` (əvvəl dublikat barkod 500 verirdi).
2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/MayaPro.WarehouseApi.SharedKernel/Application/ResultExtensions.cs` (status mapping)
- `src/MayaPro.WarehouseApi.SharedKernel/Application/Error.cs`, `Result.cs`
- `src/MayaPro.WarehouseApi.Api/Middleware/GlobalExceptionHandler.cs`
- `src/Modules/*/Domain/*Errors.cs`
