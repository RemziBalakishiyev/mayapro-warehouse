# Auth Flow

## Login

1. `POST /api/auth/login` (anonim) — `{phone, password}`.
2. `LoginHandler`: telefonla istifadəçi tapılır → yoxdursa və ya BCrypt uyğun gəlmirsə **eyni mesajla** `Auth.InvalidCredentials` (telefon mövcudluğu sızdırılmır); `IsActive=false` → `Auth.UserInactive`.
3. Uğur: JWT (HS256) + `UserDto {id, fullName, phone, role}` (rol wire kodu: `sahib`/`menecer`/`satici`).

## JWT

Claims: `sub` (user id), `name`, `role` (**enum adı**: Owner/Manager/Seller — wire kodu deyil!), `jti`. Müddət: `Jwt:ExpiryHours` (24 saat). Validation: issuer + audience + imza + lifetime, ClockSkew 1 dəq (`Api/Extensions/AuthenticationExtensions.cs`).

## Authorization

- Policy-lər host-da təyin olunur: `OwnerOnly` = RequireRole(Owner); `OwnerOrManager` = RequireRole(Owner, Manager). Modullar policy adlarını lokal `const string` kimi təkrar bəyan edir (host↔modul decoupling).
- `ICurrentUser` (scoped) JWT claim-lərindən UserId/Name verir — handler-lər satıcı adı snapshot-u üçün istifadə edir.

## İstifadəçilər

- Ayrıca qeydiyyat endpoint-i YOXDUR; istifadəçilər dev seeder-dən gəlir (`UserSeeder`, yalnız Development + boş cədvəldə): Owner `0501112233`, Manager `0552223344`, Seller `0553334455`, `0554445566` — hamısının şifrəsi `demo123`.
- `GET /api/auth/me` — cari profil; `GET /api/employees` — bütün istifadəçilər (hələlik hər rola açıq).

## Last Updated

2026-07-25 — sistem qurulanda yaradıldı.

## Related Code

- `src/Modules/MayaPro.WarehouseApi.Modules.Auth/` (Login, TokenService, UserSeeder)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs`
- `src/MayaPro.WarehouseApi.Api/Security/` (CurrentUser)
