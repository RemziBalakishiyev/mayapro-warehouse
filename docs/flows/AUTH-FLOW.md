# Auth Flow

## Login

1. `POST /api/auth/login` (anonim) — `{phone, password}`.
2. `LoginHandler`: telefonla istifadəçi tapılır → yoxdursa və ya BCrypt uyğun gəlmirsə **eyni mesajla** `Auth.InvalidCredentials` (telefon mövcudluğu sızdırılmır); `IsActive=false` → `Auth.UserInactive`. **BE#35:** telefon yalnız mağaza daxilində unikaldır, ona görə axtarış `IgnoreQueryFilters()` ilə gedir və birdən çox uyğunluqda giriş rədd olunur (`multi-tenancy.md` §4.1).
3. **BE#35/BE#36 — mağaza yoxlaması** (`PlatformAdmin` üçün atlanır): tapılmır → 403 `Auth.TenantInactiveForbidden`; təsdiq gözləyir → 403 `Auth.TenantPendingApprovalForbidden`; bloklanıb → 403 `Auth.TenantBlockedForbidden`; abunə müddəti keçib → 403 `Auth.SubscriptionExpiredForbidden`. Token verilmir. Eyni yoxlama hər sorğuda `TenantGateMiddleware`-də təkrarlanır.
4. Uğur: JWT (HS256) + `UserDto {id, fullName, phone, role}` (rol wire kodu: `sahib`/`menecer`/`satici`/`platform_admin`).

## Qeydiyyat (BE#36)

`POST /api/auth/register` (anonim) — `{storeName, ownerName, phone, password}` → `Tenant(PendingApproval)` + ilk `Owner` istifadəçi, tək transaction-da. **Token qaytarılmır** (mağaza hələ girə bilmir). Telefon bütün platforma üzrə unikal olmalıdır → təkrar 409 `Tenancy.PhoneAlreadyExists`. Endpoint Tenancy modulundadır (yaratdığı şey mağazadır; istifadəçi `IIdentityProvisioning` ilə Auth-dan istənilir). Detallar: `multi-tenancy.md` §9.2.

## JWT

Claims: `sub` (user id), `name`, `role` (**enum adı**: Owner/Manager/Seller/PlatformAdmin — wire kodu deyil!), `tenantId` (BE#35), `jti`. Müddət: `Jwt:ExpiryHours` (24 saat). Validation: issuer + audience + imza + lifetime, ClockSkew 1 dəq (`Api/Extensions/AuthenticationExtensions.cs`).

## Authorization

- Policy-lər host-da təyin olunur: `OwnerOnly` = RequireRole(Owner); `OwnerOrManager` = RequireRole(Owner, Manager); **`PlatformAdminOnly`** = RequireRole(PlatformAdmin) — yalnız `/api/admin/*`. Modullar policy adlarını lokal `const string` kimi təkrar bəyan edir (host↔modul decoupling); platforma admini üçün ad `SharedKernel.Contracts.PlatformAdminAccess`-dədir, çünki onu həm host, həm Tenancy modulu oxuyur.
- `ICurrentUser` (scoped) JWT claim-lərindən UserId/Name verir — handler-lər satıcı adı snapshot-u üçün istifadə edir.

## İstifadəçilər

- Dev seeder (`UserSeeder`, yalnız Development + boş cədvəldə): Owner `0501112233`, Manager `0552223344`, Seller `0553334455`, `0554445566` — hamısının şifrəsi `demo123`.
- **Platforma admini** (`PlatformAdminSeeder`, BE#36) — **hər mühitdə**, `PlatformAdmin` konfiqurasiya bölməsindən (telefon/şifrə/ad), idempotent. Heç bir mağazaya aid deyil (rezerv `TenantId`), ona görə mağaza datasını görmür.
- Mağaza qeydiyyatı ilk `Owner` istifadəçisini yaradır (yuxarıda).
- `GET /api/auth/me` — cari profil; `GET /api/employees` — cari mağazanın istifadəçiləri (hər rola açıq).

## Last Updated

2026-08-16 — BE#36: qeydiyyat axını, `PlatformAdmin` rolu + `PlatformAdminOnly` policy, login-in statusa görə ayrılmış 403-ləri.

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Auth/` (Login, TokenService, UserSeeder)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs`
- `src/MayaPro.WarehouseApi.Api/Security/` (CurrentUser)
