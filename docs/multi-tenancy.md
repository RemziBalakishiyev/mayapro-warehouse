# Multi-tenancy — Mərhələ 1 (data təcridi) və Mərhələ 2 (abunə + platforma admini)

**Status:** Mərhələ 1 tətbiq olunub (BE#35) · Mərhələ 2 tətbiq olunub (BE#36) · **Sonrakı:** Mərhələ 3 (plan/limit/rate-limit)

Sistem tək mağaza üçün yazılmışdı. Bu sənəd onun **çox mağazalı (multi-tenant) SaaS**-a çevrilməsini təsvir edir: eyni proqram və eyni baza bir neçə mağazaya xidmət edir, amma heç bir mağaza digərinin bir sətrini belə görmür.

İki cümləlik xülasə:

- **Mərhələ 1** — hər biznes sətrində `TenantId` var; oxumağı EF global query filter, yazmağı isə `SaveChanges` interceptor-u avtomatik məhdudlaşdırır — use case kodu tenantdan xəbərsizdir.
- **Mərhələ 2** — mağazalar özləri qeydiyyatdan keçir, platforma admini onları təsdiqləyir/bloklayır və abunə ödənişlərini yazır; abunə müddəti bitən mağaza növbəti sorğudan avtomatik bağlanır (§9).

---

## 1. Arxitektura qərarları

### 1.1 Niyə tək baza + `TenantId` sütunu?

Üç variant var idi:

| Variant | Nə deməkdir | Niyə seçilmədi / seçildi |
|---|---|---|
| Baza-per-tenant | Hər mağazaya ayrı DB | Ən güclü təcrid, amma miqrasiya, backup və bağlantı idarəsi hər yeni mağazada çoxalır; bazar seqmenti üçün əsassız əməliyyat yükü |
| Schema-per-tenant | Hər mağazaya ayrı SQL schema | Layihədə schema artıq **modul** sərhədidir (`products`, `sales`, …). Onu ikinci mənada işlətmək mövcud arxitekturanı pozardı |
| **Tək baza + `TenantId` sütunu** | Bütün mağazalar eyni cədvəllərdə, sətir səviyyəsində ayrılır | **Seçildi.** Mövcud modular monolith-ə, ortaq bağlantıya və modullararası tək transaction-a toxunmadan oturur; miqrasiya bir dəfə işləyir |

Sətir-səviyyəli təcridin məlum riski budur ki, bir unudulmuş `WHERE` bütün təcridi sındırır. Buna görə şərt **heç vaxt əl ilə yazılmır** — infrastruktur səviyyəsində, avtomatik qoyulur (§2) və avtomatlaşdırılmış test onun bütün entity-lərdə mövcudluğunu yoxlayır (§6).

### 1.2 Tenancy modulu

`Modules.Tenancy` (`tenancy` schema) mağazaların reyestridir:
`Tenants(Id, Name, OwnerName, Phone, Status, ExpiresAt, MonthlyFee, CreatedAt, UpdatedAt)`.

- `Status`: `PendingApproval` (0) · `Active` (1) · `Blocked` (2). Yalnız `Active` sistemə girə bilər.
- `ExpiresAt` / `MonthlyFee` Mərhələ 2-də əlavə olundu (§9).
- `Tenant` **tenant-scoped deyil** — təcridin özünü tərif edən cədvəldir.
- Digər modullara heç bir FK/navigation yoxdur; əlaqə həmişə sadə `TenantId` Guid-idir (eynilə `Sale.CustomerId` kimi).
- **Mərhələ 1-də HTTP endpoint açmırdı.** Mərhələ 2-də iki səth açdı: anonim qeydiyyat (`POST /api/auth/register`) və platforma admin konsolu (`/api/admin/*`).
- Başqa modullar ona yalnız `SharedKernel.Contracts.ITenantDirectory` ilə müraciət edir ("bu tenant var, girə bilərmi və müddəti nə vaxt bitir?"). Əks istiqamətdə — Tenancy → Auth — `IIdentityProvisioning` var (yeni mağazanın ilk Sahibkarını yaratmaq).

---

## 2. Mexanizm

### 2.1 `ICurrentTenant` axını

```
POST /api/auth/login
      └─ LoginHandler → tenant statusu yoxlanır → TokenService → JWT: { sub, name, role, tenantId, jti }
                                                                              │
Sonrakı hər sorğu:  Authorization: Bearer <token>                             │
      └─ UseAuthentication  → ClaimsPrincipal                                 │
      └─ TenantGateMiddleware → tenantId claim-i var? tenant Active-dir?  ◄────┘
      └─ ICurrentTenant (Api/Security/CurrentTenant.cs)
              ├─ 1) TenantScope override (yalnız anonim faktura linki)
              └─ 2) JWT "tenantId" claim-i
                        │
                        ▼
              Modul DbContext-ləri (ITenantAwareDbContext.CurrentTenantId)
                        ├─ OXUMA: global query filter  →  WHERE TenantId = @current
                        └─ YAZMA: TenantInterceptor    →  INSERT-də TenantId avtomatik
```

Fayllar:

| Rol | Fayl |
|---|---|
| Abstraksiya (`Guid? TenantId`, `bool HasTenant`) | `SharedKernel/Application/ICurrentTenant.cs` |
| HTTP implementasiyası | `Api/Security/CurrentTenant.cs` |
| Anonim yollar üçün açıq override | `SharedKernel/Application/TenantScope.cs` |
| Marker interfeys | `SharedKernel/Domain/ITenantScoped.cs` |
| Baza entity (`TenantId` `private set`) | `SharedKernel/Domain/TenantEntity.cs` |
| Default tenant sabitləri | `SharedKernel/Domain/TenantDefaults.cs` |
| Query filter mexanizmi | `SharedKernel/Infrastructure/TenantModelBuilderExtensions.cs` |
| SaveChanges interceptor | `SharedKernel/Infrastructure/TenantInterceptor.cs` |
| Sorğu qapısı | `Api/Middleware/TenantGateMiddleware.cs` |
| Reyestr kontraktı | `SharedKernel/Contracts/ITenantDirectory.cs` |

`SharedKernel` və modullar ASP.NET Core HTTP tiplərinə toxunmur — `ICurrentUser` ilə eyni pattern.

### 2.2 Oxuma: global query filter

Hər modul DbContext-i `OnModelCreating`-də bir sətir yazır:

```csharp
modelBuilder.ApplyTenantIsolation(this);
```

Bu, modeldəki **bütün** `ITenantScoped` entity-lər üçün:

- `HasQueryFilter(e => EF.Property<Guid>(e, "TenantId") == context.CurrentTenantId)`,
- `TenantId` üzərində indeks

qurur. Yeni cədvəl əlavə edən adam heç nə etməli deyil — entity `TenantEntity`-dən törəyirsə, filter özü gəlir.

**Niyə `context` (DbContext-in özü)?** EF Core query filter içindəki DbContext referansını sorğu icrası zamanı parametrə çevirir. Beləcə model bir dəfə keşlənir, amma hər sorğu öz tenant-ı ilə filtrlənir. Filtr dəyərini kənar bir servis obyektindən oxusaydıq, ilk sorğunun tenant-ı keşlənmiş modelə "donardı" — klassik və çox təhlükəli səhv.

Tenant konteksti olmayanda `CurrentTenantId == Guid.Empty` olur və filter **heç bir sətri** qaytarmır. Yəni səhv istiqamət "hamısını göstərmək" deyil, "heç nə göstərməmək"dir.

### 2.3 Yazma: `TenantInterceptor`

`AuditInterceptor` ilə eyni üslubda, hər modul DbContext-inə qoşulub:

| Vəziyyət | Davranış |
|---|---|
| `Added`, `TenantId` boş | Cari tenant avtomatik yazılır |
| `Added`, `TenantId` artıq təyin edilib | Toxunulmur (seeder / data-fix şüurlu təyin edib) |
| `Added`, tenant konteksti YOXDUR | `MissingTenantContextException` atılır — `Guid.Empty` ilə "sahibsiz sətir" yaradılmır |
| `Modified`, `TenantId` dəyişdirilib | Dəyişiklik **geri qaytarılır** (orijinal dəyər bərpa olunur, `IsModified = false`). Sətri bir mağazadan digərinə köçürmək dəstəklənən əməliyyat deyil |

**Seçilmiş davranışın əsaslandırması (AC-7):** `Modified` halında exception yerinə səssiz geri qaytarma seçildi, çünki eyni `SaveChanges` çağırışındakı digər (tamamilə qanuni) dəyişikliklər ucbatından tranzaksiyanı partlatmaq lazım deyil — nəticə eynidir: `TenantId` heç vaxt dəyişmir. `Added` halında isə əksinə, exception atılır: orada səssiz davranış sahibsiz sətir yaradardı.

Nəticədə **use case / handler kodlarında bir dənə də `Where(x => x.TenantId == ...)` yoxdur.** BE#35 heç bir handler-ə toxunmadı.

### 2.4 JWT

`TokenService` token-a `tenantId` claim-i əlavə edir (`TokenService.TenantClaim`). Claim adı xamdır, çünki bearer handler-də `MapInboundClaims = false`-dur.

`TenantGateMiddleware` (authentication-dan sonra, authorization-dan əvvəl) — sıra ilə:

| # | Hal | Cavab |
|---|---|---|
| 0 | Anonim sorğu | Buraxılır (login, **qeydiyyat**, public faktura, health, Swagger) |
| 1 | `role` claim-i `PlatformAdmin`-dir | **Buraxılır** — platforma admini heç bir mağazaya aid deyil (§9.1) |
| 2 | Autentifikasiya olunub, `tenantId` claim-i yoxdur/parse olunmur | `401` + `{ code: "Auth.TenantMissing" }` |
| 3 | Tenant tapılmır | `403` + `{ code: "Auth.TenantInactiveForbidden", message: "Mağaza aktiv deyil" }` |
| 4 | Tenant `PendingApproval` | `403` + `{ code: "Auth.TenantPendingApprovalForbidden", message: "Hesabınız təsdiq gözləyir" }` |
| 5 | Tenant `Blocked` | `403` + `{ code: "Auth.TenantBlockedForbidden", message: "Abunəliyiniz bitib — əlaqə: {admin telefonu}" }` |
| 6 | Tenant `Active`, amma `ExpiresAt` keçib | `403` + `{ code: "Auth.SubscriptionExpiredForbidden", message: "Abunəliyiniz bitib — əlaqə: {admin telefonu}" }` |

Login-də də **eyni** yoxlamalar var (`LoginHandler.EnsureTenantAllowedAsync`) və eyni kod/mesaj cütlərini qaytarır — token verilmir. Middleware-də təkrarlanır, çünki token bloklama qərarından uzun yaşayır; test hər iki cavabın mesajını bir-birinə qarşı yoxlayır.

Admin telefonu `PlatformAdmin:Phone` konfiqurasiyasındandır — kodda literal yoxdur. Konfiqurasiya olunmayıbsa mesaj «Abunəliyiniz bitib — dəstək ilə əlaqə saxlayın» olur.

**Status kodları (AC-9):** login `403`, mövcud token ilə sonrakı sorğu `403`. `401` yalnız tenant claim-i ümumiyyətlə olmayanda qaytarılır — bu, "token yaramazdır" halıdır, "mağaza qapalıdır" halı deyil.

**Qiymət:** 4–6 nömrəli yoxlamalar ƏLAVƏ sorğu tələb etmir — `ExpiresAt` mövcud `ITenantDirectory.FindAsync` nəticəsinin (`TenantInfo`) içindədir, yəni sorğu başına yenə **bir** primary-key lookup.

---

## 3. Tenant-scoped unikallıq

| Cədvəl | Əvvəl | İndi |
|---|---|---|
| `identity.Users` | `Phone` unikal | `(TenantId, Phone)` unikal |
| `products.Products` | `Barcode` unikal (filtered) | `(TenantId, Barcode)` unikal (filtered: `[Barcode] <> ''`) |
| `products.Categories` | `Name` unikal | `(TenantId, Name)` unikal |
| `expenses.ExpenseTypes` | `Name` unikal | `(TenantId, Name)` unikal |
| `dayend.Closings` | `Date` unikal | `(TenantId, Date)` unikal |
| `settings.StoreSettings` | sabit `SingletonId` ilə tək sətir | `TenantId` unikal — mağaza başına bir sətir |
| `sales.Sales` | `InvoiceToken` qlobal unikal | **dəyişmədi — qəsdən qlobal unikal** (§4.2) |

Bundan əlavə hər tenant-scoped cədvəldə sadə `TenantId` indeksi var (hər sorğu bu şərtlə başlayır).

**Yan effekt (davranış düzəlişi):** dublikat barkod əvvəllər heç yerdə yoxlanmırdı və birbaşa unique index pozuntusu → `500` verirdi. AC-10 "`500` yox" tələb etdiyi üçün yoxlama `CreateProductHandler`/`UpdateProductHandler`-ə əlavə olundu: `Products.BarcodeDuplicate` → `400`, "Bu barkod artıq mövcuddur". Yoxlama query filter üzərindən getdiyi üçün avtomatik yalnız cari mağazanın kataloquna baxır.

### 3.1 `StoreSettings` — singleton-dan mağaza-başına sətrə

`StoreSettings` sabit `SingletonId` (`1111…`) ilə tək sətir idi. İndi:

- `Create` artıq `Id`-ni sabitləmir — hər mağaza öz təsadüfi `Id`-si ilə sətir alır;
- unikallıq `TenantId` üzərindəki unique indekslə təmin olunur;
- `GET /api/settings` cari tenant-ın sətrini qaytarır, yoxdursa həmin tenant üçün default yaradır (handler kodu dəyişməyib — query filter onu artıq birmənalı edir);
- köhnə sabit id `StoreSettings.LegacySingletonId` kimi qalır; miqrasiya mövcud sətri **olduğu kimi** default mağazaya bağlayır (mağaza adı, WhatsApp şablonu, valyuta — hamısı qorunur).

---

## 4. Məlum məhdudiyyətlər və şüurlu istisnalar

### 4.1 Telefonla login-in birmənalılığı ⚠️

**Problem:** `Users.Phone` artıq yalnız mağaza daxilində unikaldır, login isə anonimdir — tenant konteksti hələ yoxdur. Deməli telefon bir neçə mağazada mövcud ola bilər.

**Həll (deterministik):** `LoginHandler` istifadəçini `IgnoreQueryFilters()` ilə axtarır və:

1. şifrə **bütün** namizədlərə qarşı yoxlanılır (erkən çıxış yoxdur);
2. şifrəsi tutan **tam bir aktiv** namizəd varsa → həmin istifadəçi ilə giriş;
3. heç biri tutmursa → `401`-ekvivalent `Auth.InvalidCredentials` ("Telefon və ya şifrə yanlışdır", HTTP `400` — mövcud kontrakt saxlanılıb);
4. birdən çox namizədin şifrəsi tutursa → **eyni** `Auth.InvalidCredentials`.

4-cü bənd şüurlu seçimdir: birini "təsadüfən" seçmək istifadəçini yad mağazaya salardı — məhz bu taskın qarşısını almaq istədiyi sızma. Heç bir halda `500` qaytarılmır.

**✅ Qalıq risk Mərhələ 2-də bağlandı.** `POST /api/auth/register` telefonu **qlobal** (bütün mağazalar üzrə) yoxlayır: artıq istifadə olunan telefonla qeydiyyat `409 Tenancy.PhoneAlreadyExists`. Deməli qeydiyyat yolundan keçən hər telefon **dəqiq bir** istifadəçini göstərir və yuxarıdakı 4-cü bənd praktikada işə düşmür. Yoxlama `IIdentityProvisioning.PhoneExistsAsync`-dədir (Auth modulu, `IgnoreQueryFilters` ilə — §5.1) və eyni qayda admin özü mağaza yaradanda (`POST /api/admin/tenants`) da tətbiq olunur.

**Yeni (kiçik) qalıq risk:** yoxlama oxu + yazıdır, ona görə eyni yeni telefonla eyni anda göndərilən iki qeydiyyat hər ikisi yoxlamadan keçə bilər. Pəncərə çox dardır və nəticə köhnə, artıq həll olunmuş qeyri-müəyyənlikdir (hər ikisi login-də rədd olunur, heç vaxt yad mağazaya düşmür). Tam bağlanması üçün register endpoint-inə **rate-limit** + platforma səviyyəli unique index lazımdır — hər ikisi Mərhələ 3-dədir (§8).

### 4.2 `InvoiceToken` qlobal unikal qalır ⚠️

`GET /api/public/invoices/{token}` **anonimdir** — WhatsApp-la paylaşılan faktura linkidir. Orada JWT yoxdur, deməli tenant konteksti də yoxdur; tenant **token-dən** həll olunmalıdır. Bunun üçün token qlobal unikal olmalıdır (32 təsadüfi bayt — toqquşma praktiki olaraq mümkünsüz).

Axın:

1. `ISalesModule.GetInvoiceTokenOwnerAsync(token)` — **yeganə** cross-tenant lookup, `IgnoreQueryFilters()` ilə. Yalnız `(SaleId, TenantId)` qaytarır.
2. `PublicInvoicePdfHandler` `TenantScope.Use(tenantId)` ilə həmin mağazanın kontekstinə girir.
3. Qalan hər şey — satış, müştəri bloku, `StoreSettings` başlığı — **adi, tam filtrlənmiş** yolla oxunur.

Yəni filter bypass olunmur; sadəcə tenant JWT yerinə token-dən qurulur və PDF yalnız fakturanı verən mağazanın məlumatlarını əks etdirir.

### 4.3 Seeder və miqrasiyalar

Startup seeder-ləri (`UserSeeder`, `ProductSeeder`, `CustomerSeeder`, `SupplierSeeder`, `ExpenseTypeSeeder` — hamısı yalnız Development; və BE#36-nın `PlatformAdminSeeder`-i — **hər mühitdə**) HTTP sorğusundan kənarda işləyir — tenant konteksti yoxdur. Hər biri iki şeyi edir:

- yazdığı sətirlərə `TenantDefaults.DefaultTenantId`-ni **açıq şəkildə** təyin edir (`AssignTenant`) — əks halda `TenantInterceptor` haqlı olaraq `MissingTenantContextException` atardı;
- "cədvəl boşdurmu?" yoxlamasını `IgnoreQueryFilters()` ilə edir — əks halda boş tenant filtrindən həmişə "boş" görünüb hər açılışda yenidən seed edərdi.

Miqrasiyalar EF query pipeline-ından tamamilə kənardadır (xam SQL) — orada filter anlayışı yoxdur; back-fill sabit default tenant id-si ilə yazılır.

> **Gələcək seeder yazanda:** bu iki addımı unutma. Unudulsa nəticə səssiz deyil — tətbiq startup-da `MissingTenantContextException` ilə dayanır.

### 4.4 Tenant statusu hər sorğuda oxunur

`TenantGateMiddleware` autentifikasiya olunmuş hər sorğuda `tenancy.Tenants`-a bir primary-key lookup edir. Mərhələ 1-də **qəsdən keşlənmir**: mağazanı bloklamaq növbəti sorğudan etibarən dərhal təsir etsin. Yük problemi olarsa qısa TTL-li `IMemoryCache` (məs. 30 s) əlavə edilə bilər — bunun bədəli bloklamanın həmin TTL qədər gecikməsidir.

### 4.5 Hələ tenant-aware olmayan şeylər

- ~~Tenant idarəetməsi yoxdur~~ → **Mərhələ 2-də gəldi** (§9).
- ~~Cross-tenant admin görünüşü yoxdur~~ → **Mərhələ 2-də gəldi**: `PlatformAdmin` rolu bütün mağazaların *reyestrini* görür. Mağazaların **datasını** (mal, satış, müştəri) heç bir rol, o cümlədən platforma admini, görmür (§9.1).
- **Plan/limit yoxdur** (Mərhələ 3): məhsul sayı, istifadəçi sayı, export həcmi kimi limitlər yoxdur.
- **Register endpoint-ində rate-limit yoxdur** (Mərhələ 3) — §4.1-in qalıq riski.
- **Tenant silinməsi/arxivləşdirilməsi və data ixracı yoxdur** (Mərhələ 3).

---

## 5. Təhlükəsizlik auditi (AC-14)

Query filter-i keçə biləcək bütün yollar araşdırıldı. Boş cədvəl qəbul edilmir — tapıntı olmayan kateqoriyada da təsdiq yazılıb.

### 5.1 `IgnoreQueryFilters()` çağırışları

| Yer | Risk | Görülən tədbir |
|---|---|---|
| `LoginHandler` — istifadəçini telefonla tapmaq | Yüksək: bütün mağazaların istifadəçilərini görür | **Şüurlu istisna.** Login anonimdir, başqa yolu yoxdur. Yalnız `Phone` üzrə filtrlənir, nəticə yalnız şifrə yoxlaması üçün istifadə olunur, heç bir sahə çölə verilmir; birdən çox uyğunluqda giriş rədd olunur (§4.1) |
| `IdentityProvisioningContract.PhoneExistsAsync` (BE#36) | Aşağı: `Any()` — heç bir sətir oxunmur | **Şüurlu istisna.** Qeydiyyat anonimdir və sual məhz "bu telefon HƏR YERDƏ tutulubmu?" sualıdır (§4.1). Yalnız `bool` qaytarır |
| `SalesModuleContract.GetInvoiceTokenOwnerAsync` | Orta: token üzrə bütün mağazaların satışlarını görür | **Şüurlu istisna.** Yalnız `(SaleId, TenantId)` qaytarır, dərhal `TenantScope` qurulur və qalan hər şey filtrlənmiş işləyir (§4.2) |
| Startup seeder-lərin boşluq yoxlaması (`UserSeeder`, `PlatformAdminSeeder`, `ProductSeeder` ×2, `CustomerSeeder`, `SupplierSeeder`, `ExpenseTypeSeeder`) | Aşağı: yalnız `Any()`, heç bir sətir oxunmur | **Şüurlu istisna.** Startup-da tenant konteksti yoxdur; əks halda hər açılışda təkrar seed edərdi (§4.3) |
| **Tenancy admin use case-ləri** (`/api/admin/*`) | — | **Bypass ehtiyacı YOXDUR.** `Tenant` və `SubscriptionPayment` `ITenantScoped` deyil, yəni onlarda söndürüləcək filtr yoxdur. Bu, `SubscriptionPayment`-i qəsdən tenant-scoped etməməyin əsas səbəbidir: bütün mağazaları görməli olan modul eyni zamanda bypass-ı olmayan modul olur |
| Digər | — | **Başqa `IgnoreQueryFilters()` çağırışı yoxdur** — bu, artıq əl ilə deyil, arxitektura testi ilə təsbit olunub (§5.1.1) |

### 5.1.1 Arxitektura testi (BE#36, AC-7)

`tests/…/IntegrationTests/IgnoreQueryFiltersArchitectureTests.cs` bütün `src/**/*.cs` mənbə fayllarını oxuyur (IL yox — mənbə, çünki nəzarət olunmalı olan şey **insanın həmin sətri yazmasıdır** və əsaslandırma fayl adının yanında görünməlidir) və:

1. allowlist-də olmayan bir `IgnoreQueryFilters(` çağırışı taparsa → **test qırılır**;
2. allowlist-də olub artıq çağırışı olmayan (və ya silinmiş) yol qalarsa → **test qırılır** (siyahı köhnəlmir);
3. skanerin özü sanity yoxlanılır (mənbə ağacını tapıb-tapmadığı).

Allowlist elə testin içindədir: hər sətir "yol + səbəb" cütüdür. Doc-comment-dəki `IgnoreQueryFilters` qeydləri saymır (yalnız kod sətirləri nəzərə alınır).

### 5.2 Raw SQL / Dapper

| Yer | Tapıntı |
|---|---|
| `IDbConnectionFactory` / `SqlConnectionFactory` | **Sorğu icra etmir.** Yalnız scope-a bir `SqlConnection` verir ki, modul DbContext-ləri eyni transaction-ı paylaşsın (BE#28). Bütün sorğular EF üzərindən gedir → filter tətbiq olunur |
| `FromSqlRaw` / `FromSqlInterpolated` / `ExecuteSqlRaw` | **Kod bazasında yoxdur** (`src/` üzrə axtarış — 0 nəticə) |
| Dapper | **Layihədə Dapper yoxdur** (`Directory.Packages.props`-da referans yoxdur) |
| Miqrasiyalardakı `migrationBuilder.Sql(...)` | Qəsdən tenant-siz: back-fill sabit default tenant id-si ilə yazır (§4.3). Runtime yolu deyil |

**Nəticə: EF filter-indən kənarda icra olunan bir dənə də runtime sorğusu yoxdur.**

### 5.3 Cross-module kontraktlar

`SharedKernel.Contracts`-dakı bütün kontraktlar öz modullarının DbContext-i üzərindən işlədiyi üçün **avtomatik tenant-scoped** oldu — implementasiyalara toxunmaq lazım gəlmədi:

| Kontrakt | Vəziyyət |
|---|---|
| `IProductsModule` | Tenant-scoped (ProductsDbContext filtri) |
| `ICustomersModule` | Tenant-scoped |
| `ISuppliersModule` | Tenant-scoped |
| `IExpensesModule` | Tenant-scoped |
| `ISalesModule` | Tenant-scoped — **istisna:** `GetInvoiceTokenOwnerAsync` (§4.2) |
| `ISettingsModule` | Tenant-scoped (mağaza başına sətir) |
| `ISalaryModule` | Tenant-scoped |
| `IDayEndModule` | Tenant-scoped |
| `IActivityLogger` | Tenant-scoped (yazarkən interceptor `TenantId` qoyur) |
| `ITenantDirectory` | **Qəsdən tenant-siz** — reyestrin özüdür; yalnız `(Id, Name, Status, IsActive)` qaytarır |

Praktiki nəticə: `POST /api/sales` sorğusunda başqa mağazanın `productId`-si göndərilsə, `IProductsModule.TryDecreaseStockAsync` məhsulu **tapa bilmir** → `404`, transaction geri sarılır, digər mağazanın stoku və müştəri borcu toxunulmaz qalır (test: `Sale_Against_Another_Shops_Product_Is_404_And_Moves_No_Stock`).

### 5.4 Tenant konteksti olmayan icra yolları

| Yol | Tapıntı / tədbir |
|---|---|
| `POST /api/auth/login` (anonim) | §4.1 — şüurlu istisna, deterministik həll |
| `GET /api/public/invoices/{token}` (anonim) | §4.2 — tenant token-dən qurulur, qalan hər şey filtrlənir |
| `GET /health` (anonim) | Data oxumur |
| `UserSeeder` (startup) | §4.3 — default tenant açıq təyin olunur |
| Miqrasiyalar | §4.3 — sabit id ilə back-fill |
| Export / import əməliyyatları | Adi autentifikasiya olunmuş sorğulardır; `IProductsModule`/`ISalesModule` üzərindən işləyirlər → tenant-scoped (test: `Product_Export_Only_Contains_The_Callers_Shop`). Import token keşi yaddaşdadır və commit yenə filtrlənmiş kontekstdə icra olunur |
| Background / scheduled iş | **Yoxdur.** Layihədə `IHostedService`, `BackgroundService` və ya planlaşdırıcı yoxdur. Gələcəkdə əlavə olunarsa, tenant kontekstini `TenantScope` ilə açıq şəkildə qurmalıdır — bu sənəd həmin qaydanı təsbit edir |
| Köhnə (tenant claim-siz) token | `TenantGateMiddleware` → `401` (test: `Token_Without_A_Tenant_Claim_Is_Rejected`) |

---

## 6. Testlər

| Sənəd | Nə yoxlayır |
|---|---|
| `tests/…/IntegrationTests/TenantIsolationApiTests.cs` | İki mağaza real HTTP API üzərindən: siyahı süzgəci, cross-tenant GET/PUT/DELETE → `404`, interceptor davranışı, body ilə spoofing, tenant-scoped unique indekslər, cross-module zəncir sızması, hesabatlar, activity, ayarlar, export, anonim faktura, bloklanmış mağaza, telefon birmənalılığı |
| `tests/…/IntegrationTests/TenantQueryFilterCoverageTests.cs` | Model səviyyəsi: hər `ITenantScoped` entity üçün query filter + `TenantId` indeksi var; `tenancy` sxemində isə YALNIZ sənədləşdirilmiş iki entity (`Tenant`, `SubscriptionPayment`) marker-sizdir; heç bir digər biznes entity marker-siz qalmayıb |
| `tests/…/IntegrationTests/PlatformAdminApiTests.cs` (BE#36) | Mərhələ 2 uçdan-uca: qeydiyyat → pending → login `403`; təsdiq → login OK; müddət keçib → istənilən authenticated sorğu `403 SubscriptionExpired`; ödəniş → dərhal yenidən işləyir; uzatma riyaziyyatı (canlı/keçmiş müddət); təkrar telefon `409`; `ExpiresAt = null` heç vaxt bloklanmır; adi Sahibkar `/api/admin/*`-a `403`; platforma admini tenant qapısından keçir, amma heç bir mağaza datasını görmür |
| `tests/…/IntegrationTests/IgnoreQueryFiltersArchitectureTests.cs` (BE#36) | Bypass allowlist-i — §5.1.1 |
| `tests/…/Modules.Tenancy.Tests/SubscriptionPeriodTests.cs` (BE#36) | `ExpiresAt` riyaziyyatı saatsız/bazasız: uzatma, təsdiq, sərhəd (dəqiq an), müddətsiz mağaza, blok/deblok müddətə toxunmur |
| `tests/…/Modules.Auth.Tests/PlatformAdminRoleTests.cs` (BE#36) | Rol adı/kodu və rezerv edilmiş tenant id-nin toqquşmaması |
| `tests/…/IntegrationTests/TenantTestFixture.cs` | İkinci/üçüncü mağazanı provizasiya edən köməkçi; BE#36-da `SetExpiryAsync`/`GetTenantAsync` əlavə olundu |

Cross-tenant müraciət **həmişə `404`**-dür, `403` deyil: `403` resursun mövcud olduğunu təsdiqləyərdi.

---

## 7. Miqrasiya (mövcud quraşdırma üçün)

Miqrasiyalar hər modulun öz tarixçəsindədir və startup-da avtomatik tətbiq olunur:

| Modul | Miqrasiya |
|---|---|
| Tenancy | `InitialTenancy` — `tenancy.Tenants` + default mağaza (`"İlk Mağaza"`, `Active`, sabit id `00000000-0000-0000-0000-000000000001`) |
| Tenancy (BE#36) | `AddSubscriptionFields` — `Tenants.ExpiresAt` (nullable), `Tenants.MonthlyFee` (`decimal(18,2)`, default 0) + yeni `tenancy.SubscriptionPayments` cədvəli |
| Auth, Products, Sales, Customers, Suppliers, Expenses, DayEnd, Activity, Settings | `AddTenantId` — `TenantId` sütunu + indekslər + **back-fill** |

**BE#36-da `identity` sxemi üçün miqrasiya YOXDUR** — bu şüurlu qərardır, bax §9.1.

**`ExpiresAt` üçün back-fill də YOXDUR:** sütun `NULL` olaraq əlavə olunur və mövcud hər mağaza (o cümlədən "İlk Mağaza") `NULL` qalır. `NULL` = **müddətsiz**, yəni heç vaxt "bitmiş" sayılmır — işləyən quraşdırma yeniləmədən sonra özünü kilidləyə bilməz. Mağaza son tarixi yalnız admin onu təsdiqləyəndə və ya ödəniş yazanda alır.

Back-fill sadə `UPDATE`-dir: `TenantId` boş olan hər sətir default mağazaya bağlanır. **Heç bir sətir silinmir, dublikat olunmur** — miqrasiyadan əvvəl və sonra sətir sayları eynidir. Hər `UPDATE`-in `WHERE [TenantId] = '000…000'` şərti var, tenant sətrinin `INSERT`-i isə `IF NOT EXISTS` ilə qorunub, ona görə təkrar icra idempotentdir.

Miqrasiyadan sonra mövcud istifadəçilər eyni telefon/şifrə ilə girməyə davam edir və bütün datalarını görür — sadəcə artıq "İlk Mağaza" adlı tenant-ın sahibi kimi.

---

## 8. Mərhələlərin vəziyyəti

### ✅ Mərhələ 1 — data təcridi (BE#35)

`TenantId` + query filter + interceptor + tenant qapısı. §1–§7.

### ✅ Mərhələ 2 — qeydiyyat, platforma admini, abunə (BE#36)

- Self-service qeydiyyat: mağaza adı + sahibkar + telefon → `Tenant(PendingApproval)` + ilk `Owner`.
- `PlatformAdmin` rolu + `PlatformAdminOnly` policy + `/api/admin/*` konsolu (təsdiq/blok/deblok/ödəniş/statistika).
- Abunə: `Tenant.ExpiresAt` + `Tenant.MonthlyFee` + `tenancy.SubscriptionPayments`; müddəti keçən mağaza avtomatik bağlanır.
- §4.1-dəki telefon birmənalılığı qeydiyyatda qlobal telefon yoxlaması ilə həll olundu.

Detallar: §9.

### ⏳ Mərhələ 3 — plan, limit və möhkəmləndirmə

- **Register endpoint-inə rate-limit** (IP başına, mövcud `PublicInvoice` policy-si kimi) — §4.1-in qalıq yarışı və spam qeydiyyat üçün. **Mərhələ 2-də qəsdən edilmədi**, çünki düzgün ölçü (IP? telefon prefiksi? captcha?) məhsul qərarıdır.
- Platforma səviyyəli telefon unikallığı (filtrli unique index) — yuxarıdakı ilə birlikdə.
- Plan (`Free` / `Pro` / …), limitlər (məhsul sayı, istifadəçi sayı, export həcmi).
- İstifadə ölçmə və avtomatik faktura/qəbz.
- Tenant silinməsi/arxivləşdirilməsi və data ixracı.
- Tenant statusu üçün qısa TTL-li keş (§4.4) — yük problemi yaranarsa.

---

## 9. Mərhələ 2 — qeydiyyat, platforma admini və abunə (BE#36)

### 9.1 Platforma admini: `TenantId` necə həll olundu

Platforma operatoru heç bir mağazaya aid deyil, amma `identity.Users`-dakı `TenantId` sütunu `NOT NULL`-dur və `(TenantId, Phone)` unikal indeksi var. Üç variant vardı:

| Variant | Nəticə | Qərar |
|---|---|---|
| `TenantId` nullable etmək | `TenantEntity`, query filter mexanizmi, unikal indeks — hamısı dəyişməli; işləyən quraşdırmada miqrasiya riski | **Seçilmədi** — mərkəzi abstraksiyanı bir istifadəçi üçün deşmək |
| `Guid.Empty` | `TenantInterceptor` `Guid.Empty`-ni "təyin edilməyib" kimi oxuyur → seed-də `MissingTenantContextException`; üstəlik tenant konteksti boş olan sorğuda filter məhz bu sətri **tapır** — "boş kontekst heç nə görmür" invariantı pozulur | **Seçilmədi** |
| **Rezerv edilmiş id** `00000000-0000-0000-0000-0000000000ff` (`TenantDefaults.PlatformTenantId`) | Sxem dəyişmir, interceptor kontraktı pozulmur, heç bir `tenancy.Tenants` sətri bu id-ni istifadə etmir → bu id altında **bir dənə də biznes sətri yoxdur** → hər tenant-scoped sorğu boş qayıdır (fail-closed) | **Seçildi** |

Əlavə üstünlük: adminin **öz** `identity.Users` sətri bu id altındadır, ona görə `GET /api/auth/me` heç bir bypass olmadan işləyir (`NULL` variantında `404` olardı).

Nəticədə admin üçün:

- token-də `tenantId` claim-i **var** (rezerv id), amma tenant qapısı onu **rola görə** buraxır — `role = PlatformAdmin` (§2.4, sətir 1). Bu, yeganə istisnadır və yalnız bu rola aiddir: tenant claim-siz **adi** token hələ də `401` alır (test: `Token_Without_A_Tenant_Claim_Is_Rejected`);
- `/api/admin/*` `PlatformAdminOnly` policy-si ilə qorunur — adi `Owner` (Sahibkar) `403` alır;
- `/api/products`, `/api/customers` və s. sorğuları `200` qaytarır, amma **boş** — sızma yoxdur (test: `Platform_Admin_Passes_The_Tenant_Gate_But_Sees_No_Shop_Data`).

Seed: `PlatformAdminSeeder` (`PlatformAdmin` konfiqurasiya bölməsindən telefon/şifrə/ad), **hər mühitdə** işləyir (əks halda təzə production quraşdırmasının konsola girişi olmazdı), **idempotentdir** (mövcud admin varsa heç nə etmir və şifrəsini yenidən yazmır) və bölmə konfiqurasiya olunmayıbsa **heç nə seed etmir** (təxmin edilə bilən default parol yaranmasın).

> **Production:** `PlatformAdmin__Password` (və `PlatformAdmin__Phone`) mühit dəyişəni ilə override edin. `appsettings.json`-dakı dəyər yalnız dev üçündür.

### 9.2 Qeydiyyat axını

```
POST /api/auth/register   (ANONİM)
  { storeName, ownerName, phone, password }
        │
        ├─ validasiya (ad/telefon uzunluqları, şifrə ≥ 6)
        ├─ IIdentityProvisioning.PhoneExistsAsync(phone)   ← QLOBAL, §4.1
        │      └─ tapıldı → 409 Tenancy.PhoneAlreadyExists
        │
        └─ IUnitOfWork transaction:
               tenancy.Tenants   ← Tenant(PendingApproval)
               identity.Users    ← User(Owner, TenantId AÇIQ təyin olunur)
        →  201 { tenantId, storeName, status: "PendingApproval", message }
```

- **Token verilmir** — mağaza hələ girə bilmir; login `403 Auth.TenantPendingApprovalForbidden` ("Hesabınız təsdiq gözləyir") deyir.
- İki fərqli sxemə yazıldığı üçün `TenancyDbContext` BE#36-da `ITransactionalDbContext` oldu: "sahibi olmayan mağaza" və ya "mağazası olmayan sahib" əldə edilə bilən vəziyyət deyil.
- Yeni istifadəçinin `TenantId`-si **açıq şəkildə** təyin olunur (`AssignTenant`) — qeydiyyat anonimdir, ambient tenant yoxdur və `TenantInterceptor` haqlı olaraq `MissingTenantContextException` atardı (§4.3-dəki seeder qaydasının eynisi).
- Eyni provizasiya kodu (`TenantProvisioning`) admin `POST /api/admin/tenants` çağıranda da işləyir — fərq yalnız statusdur (`Active`) və opsional müddətdir.

### 9.3 Abunə və `ExpiresAt` semantikası

| Dəyər | Məna |
|---|---|
| `ExpiresAt = null` | **Müddətsiz** — HEÇ VAXT bitmiş sayılmır |
| `ExpiresAt > now` | Abunə qüvvədədir |
| `ExpiresAt <= now` | **Bitib** — status dəyişmir, amma giriş bağlanır |

Sərhəd **inklüzivdir**: `ExpiresAt == now` artıq bitmiş sayılır.

İki fərqli əməliyyat:

| Əməliyyat | Düstur | Niyə |
|---|---|---|
| **Təsdiq** (`approve`) | `ExpiresAt = now + N ay` | Təsdiq təmiz başlanğıcdır; köhnə (çox güman köhnəlmiş) tarixin üstünə qurmaq yanlış olardı |
| **Ödəniş** (`payments`) | `ExpiresAt = max(now, mövcud ExpiresAt ?? now) + N ay` | Vaxtından əvvəl ödəyən müştəri qalan günlərini itirmir; gecikən müştəri isə qismən keçmişdə olan müddət almır |

Bütün tarix hesabı `Tenant` domain entity-sindədir və "indi"ni **arqument kimi** alır; handler-lər `IDateProvider.UtcNow` ötürür (kodda birbaşa `DateTime.UtcNow` yoxdur), ona görə riyaziyyat saatsız unit testlə örtülüb (`SubscriptionPeriodTests`).

### 9.4 Avto-blok mexanizmi

- Yoxlama **mövcud** tenant lookup-ının içindədir (`TenantGateMiddleware`, §2.4 sətir 6) — nə yeni middleware, nə ikinci sorğu.
- **Status DƏYİŞMİR.** `ExpiresAt` özü hökmdür. Bunun üç faydası var: fon işi (scheduler) lazım deyil; admin ödəniş yazan kimi mağaza **növbəti sorğuda** açılır (yenidən login belə tələb olunmur — token dəyişmir); "bloklanıb, amma niyə?" kimi bərpa ediləsi vəziyyət yaranmır.
- Support-un əl ilə qoyduğu `Blocked` statusu **ayrıdır**: ödəniş onu açmır. Blok — support qərarı, `ExpiresAt` — billing.
- `GET /api/admin/stats`-dakı `expiredCount` məhz "Active, amma müddəti keçib" mağazaları sayır.

### 9.5 Admin endpoint-ləri

Hamısı `/api/admin/*`, hamısı `PlatformAdminOnly`. Siyahı və validasiya qaydaları: `docs/api/API-OVERVIEW.md`.

`SubscriptionPayment` **qəsdən `ITenantScoped` deyil**: bu, platforma səviyyəli billing qeydidir, mağaza datası deyil. Onu tenant-scoped etsəydik, tenant konteksti olmayan admin heç nə görməzdi və məhz bypass-sız olmalı modula `IgnoreQueryFilters()` gətirməli olardıq (§5.1).

---

## Last Updated

2026-08-16 — BE#36 (Mərhələ 2: qeydiyyat, platforma admini, abunə və avto-blok).

2026-08-16 — BE#35 (Mərhələ 1: data təcridi).
