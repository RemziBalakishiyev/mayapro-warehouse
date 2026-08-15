# Multi-tenancy — Mərhələ 1: data təcridi

**Status:** tətbiq olunub (BE#35) · **Əhatə:** data təcridi · **Sonrakılar:** Mərhələ 2 (tenant qeydiyyatı), Mərhələ 3 (plan/billing)

Sistem tək mağaza üçün yazılmışdı. Bu mərhələ onu **çox mağazalı (multi-tenant) SaaS**-a çevirir: eyni proqram və eyni baza bir neçə mağazaya xidmət edir, amma heç bir mağaza digərinin bir sətrini belə görmür.

Bir cümləlik xülasə: **hər biznes sətrində `TenantId` var; oxumağı EF global query filter, yazmağı isə `SaveChanges` interceptor-u avtomatik məhdudlaşdırır — use case kodu tenantdan xəbərsizdir.**

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

`Modules.Tenancy` (`tenancy` schema) mağazaların reyestridir: `Tenants(Id, Name, OwnerName, Phone, Status, CreatedAt, UpdatedAt)`.

- `Status`: `PendingApproval` (0) · `Active` (1) · `Blocked` (2). Yalnız `Active` sistemə girə bilər.
- `Tenant` **tenant-scoped deyil** — təcridin özünü tərif edən cədvəldir.
- Digər modullara heç bir FK/navigation yoxdur; əlaqə həmişə sadə `TenantId` Guid-idir (eynilə `Sale.CustomerId` kimi).
- **Mərhələ 1-də HTTP endpoint açmır.** Mağaza yaratmaq/idarə etmək Mərhələ 2-nin işidir.
- Başqa modullar ona yalnız `SharedKernel.Contracts.ITenantDirectory` ilə müraciət edir ("bu tenant var və girə bilərmi?").

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

`TenantGateMiddleware` (authentication-dan sonra, authorization-dan əvvəl):

| Hal | Cavab |
|---|---|
| Anonim sorğu | Buraxılır (login, public faktura, health, Swagger) |
| Autentifikasiya olunub, `tenantId` claim-i yoxdur/parse olunmur | `401` + `{ code: "Auth.TenantMissing" }` |
| Tenant tapılmır və ya `Active` deyil | `403` + `{ code: "Auth.TenantInactiveForbidden", message: "Mağaza aktiv deyil" }` |

Login-də də eyni yoxlama var (`AuthErrors.TenantInactiveForbidden` → `403`, token verilmir). Middleware-də təkrarlanır, çünki token bloklama qərarından uzun yaşayır.

**Status kodları (AC-9):** login `403`, mövcud token ilə sonrakı sorğu `403`. `401` yalnız tenant claim-i ümumiyyətlə olmayanda qaytarılır — bu, "token yaramazdır" halıdır, "mağaza qapalıdır" halı deyil.

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

**Qalıq risk:** eyni telefon + eyni şifrə ilə iki mağazada qeydiyyatdan keçmiş istifadəçi heç birinə girə bilmir. Mərhələ 2-də qeydiyyat forması bu vəziyyəti (məs. mağaza seçimi addımı və ya qeydiyyatda qlobal telefon yoxlaması ilə) həll etməlidir.

### 4.2 `InvoiceToken` qlobal unikal qalır ⚠️

`GET /api/public/invoices/{token}` **anonimdir** — WhatsApp-la paylaşılan faktura linkidir. Orada JWT yoxdur, deməli tenant konteksti də yoxdur; tenant **token-dən** həll olunmalıdır. Bunun üçün token qlobal unikal olmalıdır (32 təsadüfi bayt — toqquşma praktiki olaraq mümkünsüz).

Axın:

1. `ISalesModule.GetInvoiceTokenOwnerAsync(token)` — **yeganə** cross-tenant lookup, `IgnoreQueryFilters()` ilə. Yalnız `(SaleId, TenantId)` qaytarır.
2. `PublicInvoicePdfHandler` `TenantScope.Use(tenantId)` ilə həmin mağazanın kontekstinə girir.
3. Qalan hər şey — satış, müştəri bloku, `StoreSettings` başlığı — **adi, tam filtrlənmiş** yolla oxunur.

Yəni filter bypass olunmur; sadəcə tenant JWT yerinə token-dən qurulur və PDF yalnız fakturanı verən mağazanın məlumatlarını əks etdirir.

### 4.3 Seeder və miqrasiyalar

Beş dev seeder (`UserSeeder`, `ProductSeeder`, `CustomerSeeder`, `SupplierSeeder`, `ExpenseTypeSeeder`) startup-da, HTTP sorğusundan kənarda işləyir — tenant konteksti yoxdur. Hər biri iki şeyi edir:

- yazdığı sətirlərə `TenantDefaults.DefaultTenantId`-ni **açıq şəkildə** təyin edir (`AssignTenant`) — əks halda `TenantInterceptor` haqlı olaraq `MissingTenantContextException` atardı;
- "cədvəl boşdurmu?" yoxlamasını `IgnoreQueryFilters()` ilə edir — əks halda boş tenant filtrindən həmişə "boş" görünüb hər açılışda yenidən seed edərdi.

Miqrasiyalar EF query pipeline-ından tamamilə kənardadır (xam SQL) — orada filter anlayışı yoxdur; back-fill sabit default tenant id-si ilə yazılır.

> **Gələcək seeder yazanda:** bu iki addımı unutma. Unudulsa nəticə səssiz deyil — tətbiq startup-da `MissingTenantContextException` ilə dayanır.

### 4.4 Tenant statusu hər sorğuda oxunur

`TenantGateMiddleware` autentifikasiya olunmuş hər sorğuda `tenancy.Tenants`-a bir primary-key lookup edir. Mərhələ 1-də **qəsdən keşlənmir**: mağazanı bloklamaq növbəti sorğudan etibarən dərhal təsir etsin. Yük problemi olarsa qısa TTL-li `IMemoryCache` (məs. 30 s) əlavə edilə bilər — bunun bədəli bloklamanın həmin TTL qədər gecikməsidir.

### 4.5 Hələ tenant-aware olmayan şeylər

- **Tenant idarəetməsi yoxdur** — mağaza yaratmaq/aktivləşdirmək/bloklamaq üçün endpoint yoxdur (Mərhələ 2). Hazırda bu, bazada birbaşa `tenancy.Tenants` sətri ilə edilir.
- **Plan/limit/billing yoxdur** (Mərhələ 3).
- **Cross-tenant admin görünüşü yoxdur** — heç bir rol bütün mağazaları görə bilmir. `Owner` da yalnız öz mağazasını görür.

---

## 5. Təhlükəsizlik auditi (AC-14)

Query filter-i keçə biləcək bütün yollar araşdırıldı. Boş cədvəl qəbul edilmir — tapıntı olmayan kateqoriyada da təsdiq yazılıb.

### 5.1 `IgnoreQueryFilters()` çağırışları

| Yer | Risk | Görülən tədbir |
|---|---|---|
| `LoginHandler` — istifadəçini telefonla tapmaq | Yüksək: bütün mağazaların istifadəçilərini görür | **Şüurlu istisna.** Login anonimdir, başqa yolu yoxdur. Yalnız `Phone` üzrə filtrlənir, nəticə yalnız şifrə yoxlaması üçün istifadə olunur, heç bir sahə çölə verilmir; birdən çox uyğunluqda giriş rədd olunur (§4.1) |
| `SalesModuleContract.GetInvoiceTokenOwnerAsync` | Orta: token üzrə bütün mağazaların satışlarını görür | **Şüurlu istisna.** Yalnız `(SaleId, TenantId)` qaytarır, dərhal `TenantScope` qurulur və qalan hər şey filtrlənmiş işləyir (§4.2) |
| Dev seeder-lərin boşluq yoxlaması (`UserSeeder`, `ProductSeeder` ×2, `CustomerSeeder`, `SupplierSeeder`, `ExpenseTypeSeeder`) | Aşağı: yalnız `Any()`, heç bir sətir oxunmur | **Şüurlu istisna.** Startup-da tenant konteksti yoxdur; əks halda hər açılışda təkrar seed edərdi (§4.3) |
| Digər | — | **Başqa `IgnoreQueryFilters()` çağırışı yoxdur** (bütün `src/` yoxlanıldı) |

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
| `tests/…/IntegrationTests/TenantQueryFilterCoverageTests.cs` | Model səviyyəsi: hər `ITenantScoped` entity üçün query filter + `TenantId` indeksi var; `Tenant` isə filtrlənməyib; heç bir biznes entity marker-siz qalmayıb |
| `tests/…/IntegrationTests/TenantTestFixture.cs` | İkinci/üçüncü mağazanı provizasiya edən köməkçi (Mərhələ 2-də qeydiyyat endpoint-i gələndə bunun yerini tutacaq) |

Cross-tenant müraciət **həmişə `404`**-dür, `403` deyil: `403` resursun mövcud olduğunu təsdiqləyərdi.

---

## 7. Miqrasiya (mövcud quraşdırma üçün)

Miqrasiyalar hər modulun öz tarixçəsindədir və startup-da avtomatik tətbiq olunur:

| Modul | Miqrasiya |
|---|---|
| Tenancy | `InitialTenancy` — `tenancy.Tenants` + default mağaza (`"İlk Mağaza"`, `Active`, sabit id `00000000-0000-0000-0000-000000000001`) |
| Auth, Products, Sales, Customers, Suppliers, Expenses, DayEnd, Activity, Settings | `AddTenantId` — `TenantId` sütunu + indekslər + **back-fill** |

Back-fill sadə `UPDATE`-dir: `TenantId` boş olan hər sətir default mağazaya bağlanır. **Heç bir sətir silinmir, dublikat olunmur** — miqrasiyadan əvvəl və sonra sətir sayları eynidir. Hər `UPDATE`-in `WHERE [TenantId] = '000…000'` şərti var, tenant sətrinin `INSERT`-i isə `IF NOT EXISTS` ilə qorunub, ona görə təkrar icra idempotentdir.

Miqrasiyadan sonra mövcud istifadəçilər eyni telefon/şifrə ilə girməyə davam edir və bütün datalarını görür — sadəcə artıq "İlk Mağaza" adlı tenant-ın sahibi kimi.

---

## 8. Gələcək mərhələlər

### Mərhələ 2 — tenant qeydiyyatı və idarəsi

- Self-service qeydiyyat: mağaza adı + sahibkar + telefon → `Tenant(PendingApproval)` + ilk `Owner` istifadəçi.
- Təsdiq/bloklama üçün admin səthi (`Active` / `Blocked` keçidləri).
- §4.1-dəki telefon birmənalılığı problemi qeydiyyat mərhələsində həll olunmalıdır (mağaza seçimi addımı və ya qeydiyyatda telefonun qlobal yoxlanması).
- Tenant silinməsi/arxivləşdirilməsi və data ixracı.

### Mərhələ 3 — plan və billing

- Plan (`Free` / `Pro` / …), limitlər (məhsul sayı, istifadəçi sayı, export həcmi).
- Abunə vəziyyəti → `TenantStatus` ilə əlaqə (ödəniş gecikəndə avtomatik `Blocked`).
- İstifadə ölçmə və faktura.

---

## Last Updated

2026-08-16 — BE#35 (Mərhələ 1: data təcridi).
