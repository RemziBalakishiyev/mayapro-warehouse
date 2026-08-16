# QA Report — BE#36: Multi-tenant SaaS Mərhələ 2 (abunə sistemi və platforma admin API-si)

**Tarix:** 2026-08-16
**QA Agent:** qa-tester
**Test edilən PR:** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/39 (branch `task/BE#36-abune-ve-platforma-admin`, commit `5cf5e30`)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/36
**AC/TC mənbəyi:** issue #36-dakı pm-agent şərhi — 26 Acceptance Criteria (AC-1…AC-26) + 22 Test Case (TC-1…TC-22)
**Mühit:** Lokal, Windows 11 Pro, .NET 8 SDK, SQL Server (`MayaProWarehouse_Test`), host mədəniyyəti `az-Latn-AZ`

> Qeyd: `src/MayaPro.WarehouseApi.Api/appsettings.json`-dakı commit olunmamış lokal dəyişikliyə (`Cors:FrontendOrigin = http://localhost:5177`) toxunulmayıb.

---

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Acceptance Criteria | 26 — **24 ✅ / 2 ❌** (2-si hərfi deviasiya ilə: AC-2, AC-5) |
| Test case | 22 — **21 ✅ / 1 ❌ / 0 ⚠️** |
| Avtomatlaşdırılmış testlər | **694/694 yaşıl** (0 fail, 0 skip) |
| Tapılan bug | **3** (1 × Medium funksional, 2 × Medium kontrakt deviasiyası) |
| Bloklamayan müşahidə | 4 (OBS-1…OBS-4) |
| **Yekun qərar** | **QA FAILED → In Progress** (BUG-1 funksionaldır, AC-14/TC-18 pozulur) |

Ümumi keyfiyyət yüksəkdir: təhlükəsizlik modeli (fail-closed `PlatformTenantId` sentinel, bypass-sız admin səthi, arxitektura testi), abunə riyaziyyatı və tranzaksiya atomikliyi müstəqil olaraq təsdiqləndi. Geri qaytarılmanın yeganə funksional səbəbi mağaza axtarışının Azərbaycan mədəniyyətində sınmasıdır (BUG-1); qalan iki bənd frontend-lə razılaşdırılmalı wire adlandırma deviasiyalarıdır.

---

## 1. İcra olunan əmrlər və nəticələri

### 1.1 Build

```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:01:07.74
```

### 1.2 Tam test paketi (bu QA sessiyasında əlavə olunan 6 QA testi ilə birlikdə)

```
$ dotnet test
Passed! - Failed: 0, Passed:  43, Skipped: 0, Total:  43 - SharedKernel.Tests.dll
Passed! - Failed: 0, Passed:   8, Skipped: 0, Total:   8 - Modules.Tenancy.Tests.dll
Passed! - Failed: 0, Passed:  57, Skipped: 0, Total:  57 - Modules.Reports.Tests.dll
Passed! - Failed: 0, Passed:   9, Skipped: 0, Total:   9 - Modules.DayEnd.Tests.dll
Passed! - Failed: 0, Passed:  12, Skipped: 0, Total:  12 - Modules.Suppliers.Tests.dll
Passed! - Failed: 0, Passed:  54, Skipped: 0, Total:  54 - Modules.Expenses.Tests.dll
Passed! - Failed: 0, Passed:  46, Skipped: 0, Total:  46 - Modules.Exports.Tests.dll
Passed! - Failed: 0, Passed:  20, Skipped: 0, Total:  20 - Modules.Customers.Tests.dll
Passed! - Failed: 0, Passed:  50, Skipped: 0, Total:  50 - Modules.Sales.Tests.dll
Passed! - Failed: 0, Passed:  54, Skipped: 0, Total:  54 - Modules.Auth.Tests.dll
Passed! - Failed: 0, Passed:  74, Skipped: 0, Total:  74 - Modules.Products.Tests.dll
Passed! - Failed: 0, Passed: 267, Skipped: 0, Total: 267 - IntegrationTests.dll (1 m 2 s)
```

- **Cəm: 694/694 yaşıl, 0 uğursuz, 0 skipped.**
- Developer/senior-in bildirdiyi baza **688** idi; bu sessiyada QA `tests/MayaPro.WarehouseApi.IntegrationTests/Be36QaGapTests.cs` faylı ilə **+6 test** əlavə etdi (688 → 694). Baza rəqəmi olduğu kimi təsdiqləndi.
- İnteqrasiya testləri **real host + real SQL Server** üzərində işləyir (`WarehouseApiFactory` bazanı silib yenidən miqrasiya edir, sonra `/health` ilə hostu qaldırır) → bütün admin/abunə axını faktiki HTTP + faktiki DB üzərində yoxlanılıb. Ayrıca `dotnet run` ilə canlı smoke icra OLUNMADI — səbəb §6-da.

### 1.3 QA-nın əlavə etdiyi testlər (PR-a daxil edildi)

`tests/MayaPro.WarehouseApi.IntegrationTests/Be36QaGapTests.cs` — TC siyahısında olub commit olunmuş paketin birbaşa iddia etmədiyi hallar:

```
Passed Be36QaGapTests.Tc21_A_Failed_Owner_Creation_Leaves_No_Orphan_Tenant
Passed Be36QaGapTests.Tc12_A_Blocked_Shop_Is_Refused_At_Login_With_The_Configured_Phone
Passed Be36QaGapTests.Tc15_A_Negative_Period_Is_Rejected_And_Writes_Nothing
Passed Be36QaGapTests.Tc16_Payment_History_Is_Per_Shop_And_Newest_First
Passed Be36QaGapTests.Tc17_This_Months_Takings_Exclude_A_Payment_From_Last_Month
Passed Be36QaGapTests.Platform_Admin_Reads_No_Sales_Either
6/6 yaşıl
```

TC-18 üçün reqressiya testi qəsdən əlavə edilmədi — o, BUG-1-in düzəlişi ilə birlikdə gəlməlidir (qırmızı test branch-a commit edilmir).

### 1.4 Müvəqqəti probe-lar (icra olundu, sonra silindi)

- `src/Modules/MayaPro.WarehouseApi.Modules.Products/QaTemporaryBypassProbe.cs` — TC-20/AC-23 üçün süni `IgnoreQueryFilters()` pozuntusu.
- `tests/MayaPro.WarehouseApi.IntegrationTests/CultureProbe.cs` — BUG-1-in kök səbəbi üçün mədəniyyət ölçməsi.

Hər ikisi ölçmədən sonra silindi; `git status` `src/` altında yalnız istifadəçinin öz `appsettings.json` dəyişikliyini göstərir.

---

## 2. Acceptance Criteria matrisi

| AC | Nəticə | Örtən test / dəlil |
|---|---|---|
| **AC-1** Rol `PlatformAdmin = 4`, `ToCode` partlamır | ✅ | `Modules.Auth.Tests/PlatformAdminRoleTests.Every_Role_Maps_To_A_Code`, `.The_Wire_Codes_Are_Additive_Not_Substituted`; funksional: `PlatformAdminApiTests.Platform_Admin_Can_Read_Its_Own_Profile_And_Carries_The_Platform_Role` → `GET /api/auth/me` **200** (500 deyil), `role = "platform_admin"` |
| **AC-2** Tenant-sız identiklik | ✅ (deviasiya, sənədləşdirilib) | `TenantDefaults.PlatformTenantId = …-0000000000ff`. AC hərfən `Guid.Empty` deyirdi; developer rezerv id seçdi, çünki `Guid.Empty` `TenantInterceptor`-un «boş = təyin edilməyib» kontraktını pozur. Qərar `docs/multi-tenancy.md` §9.1-də yazılıb. Test: `PlatformAdminRoleTests.The_Platform_Tenant_Id_Collides_With_Nothing` |
| **AC-3** Tenant qapısından azadlıq | ✅ | `TenantGateMiddleware` (rol yoxlaması ən başda); `PlatformAdminApiTests.Platform_Admin_Passes_The_Tenant_Gate_But_Sees_No_Shop_Data`; BE#35 reqressiyası: `TenantIsolationApiTests.Token_Without_A_Tenant_Claim_Is_Rejected` yaşıl |
| **AC-4** Seed, konfiqurasiyadan, idempotent | ✅ (OBS-1) | `PlatformAdminSeeder`; `PlatformAdminApiTests.The_Platform_Admin_Is_Seeded_Exactly_Once`. Konfiqurasiya yoxdursa seed işləmir və tətbiq partlamır — **lakin log warning yazılmır** (AC hərfən tələb edir) → OBS-1 |
| **AC-5** `PlatformAdminOnly` policy | ✅ (deviasiya) | `AuthenticationExtensions` policy-ni `PlatformAdminAccess.RoleName` sabiti ilə qurur; host-dakı lokal `Roles` mirror enum-una `PlatformAdmin` **əlavə edilməyib**. Funksional nəticə eynidir (hər ikisi `"PlatformAdmin"` sətrini verir) və SharedKernel sabiti modul↔host uyğunsuzluğuna qarşı daha güclüdür. Funksional təsdiq: anonim → 401, Owner/Manager/Seller → 403, `/api/auth/me` → 200 |
| **AC-6** Bypass yalnız admin use case-lərində | ✅ | `Tenant` və `SubscriptionPayment` `Entity`-dən törəyir (`TenantEntity` deyil); `TenantQueryFilterCoverageTests.Only_The_Documented_Tenancy_Entities_Escape_The_Tenant_Marker`, `.The_Tenant_Registry_Itself_Is_Not_Filtered`. Tenancy admin səthində bir dənə də `IgnoreQueryFilters` yoxdur |
| **AC-7** `POST /api/auth/register` anonim, 201, tokensiz | ✅ | `PlatformAdminApiTests.Registration_Creates_A_Pending_Shop_Whose_Owner_Cannot_Sign_In_Yet`, `.Registration_Rejects_An_Empty_Store_Name_And_A_Short_Password` |
| **AC-8** Bir tranzaksiyada Tenant + Owner | ✅ | `TenantProvisioning` → `IUnitOfWork.BeginTransactionAsync` (paylaşılan connection, bütün `ITransactionalDbContext`-lər enlist olunur, commit olunmayan tranzaksiya `DisposeAsync`-də geri sarılır). `AddOwnerAsync` `AssignTenant` ilə açıq təyinat edir → `MissingTenantContextException` atılmır. Atomiklik: `Be36QaGapTests.Tc21_…` |
| **AC-9** Təkrar telefon → 409, qlobal | ✅ | `IdentityProvisioningContract.PhoneExistsAsync` (`IgnoreQueryFilters`, `Any()`, sətir oxumur); `Registering_An_Already_Used_Phone_Is_409`, `Registration_Is_Refused_For_A_Phone_That_Already_Logs_In` |
| **AC-10** Login-də status ayrımı | ✅ | `LoginHandler.EnsureTenantAllowedAsync`; `Registration_Creates_A_Pending_Shop…` (Pending), `Be36QaGapTests.Tc12_…` (Blocked) |
| **AC-11** `ExpiresAt` + `MonthlyFee`, migration toxunmur | ✅ | `Tenant` (private setter, domain metodları), miqrasiya `20260815234449_AddSubscriptionFields`; `A_Shop_Without_An_Expiry_Is_Never_Auto_Blocked` (default mağaza `ExpiresAt = null` işləyir) |
| **AC-12** `SubscriptionPayment` cədvəli | ✅ (OBS-2) | `SubscriptionPaymentConfiguration`, `decimal(18,2)`, `TenantId` indeksi; `Recording_A_Payment_Reopens_An_Expired_Shop` bütün sahələri yoxlayır. `RecordedByAdminId` `Guid?`-dir (AC `Guid` deyirdi) → OBS-2 |
| **AC-13** `max(now, mövcud ?? now) + N ay` | ✅ | `Modules.Tenancy.Tests/SubscriptionPeriodTests` (7 test, saatsız); HTTP səviyyəsində `Payment_Adds_To_A_Live_Period_And_Restarts_A_Lapsed_One` |
| **AC-14** 8 admin endpoint-i | ❌ | 8 endpoint-in hamısı mövcuddur və cavab verir, **lakin 1-ci sətrin «`search` … case-insensitive» tələbi pozulur** → **BUG-1**. Əlavə: `approve`/`payments` body sahəsi `months`-dur, AC `periodMonths` deyir; `stats` sahəsi `thisMonthCollected`-dir, AC `collectedThisMonth` deyir → **BUG-3** |
| **AC-15** Kontrakt intizamı (route, camelCase, ISO, `{code,message}`, 404/400) | ✅ | `Approve_Validates_Its_Input_And_Its_Target`, `Payment_Validates_Amount_Period_And_Target`, `Block_And_Unblock…` (404-lər); pul JSON-da number |
| **AC-16** Middleware 403 + `code = "SubscriptionExpired"` | ❌ | Davranış tam düzgündür (403, status dəyişmir, mesajda konfiqurasiyadan gələn telefon), **lakin `code` = `Auth.SubscriptionExpiredForbidden`**, AC «məhz `SubscriptionExpired`» tələb edir → **BUG-2**. Test: `An_Expired_Subscription_Blocks_Every_Authenticated_Call` |
| **AC-17** `ExpiresAt == null` bloklamır | ✅ | `A_Shop_Without_An_Expiry_Is_Never_Auto_Blocked`; `SubscriptionPeriodTests.An_Open_Ended_Shop_Is_Never_Expired` |
| **AC-18** Login-də eyni yoxlama | ✅ | `An_Expired_Subscription_Blocks_Every_Authenticated_Call` (sonundakı login hissəsi), `The_Expiry_Message_Names_The_Configured_Support_Phone` |
| **AC-19** Admin özü kilidlənmir | ✅ | `TenantGateMiddleware` rol yoxlaması bütün digər yoxlamalardan əvvəldir; `LoginHandler` PlatformAdmin üçün tenant yoxlamasını tam ötürür. Bütün admin testləri müddət dolmuş mağazalar mövcud olarkən işləyir |
| **AC-20** Əlavə DB gediş-gəlişi yoxdur | ✅ | Mənbə oxuması: `TenantInfo` artıq `ExpiresAt` daşıyır (`ITenantDirectory`), yoxlama mövcud `FindAsync` nəticəsi üzərində aparılır — ikinci sorğu yoxdur |
| **AC-21** Arxitektura testi | ✅ | `IgnoreQueryFiltersArchitectureTests` (3 test) |
| **AC-22** Allow-list dəqiqdir | ✅ | 9 yol, hər biri əsaslandırma sətri ilə; `The_Allowlist_Contains_No_Stale_Entries` əks istiqaməti də qoruyur |
| **AC-23** Test öz-özünü yoxlayır | ✅ | **QA tərəfindən canlı təkrarlandı** — bax §4 (TC-20) |
| **AC-24** `docs/multi-tenancy.md` yenilənib | ✅ | §9 (9.1–9.5) + §5.1.1 + §8 Mərhələ 2 bölməsi mövcuddur |
| **AC-25** Rate-limit qeydi (məcburi) | ✅ | `docs/multi-tenancy.md` §8 sətir 218 və Mərhələ 3 bölməsi (sətir 346): «Register endpoint-ində rate-limit yoxdur (Mərhələ 3)», qalıq yarış riski §4.1-də (sətir 184); `API-OVERVIEW.md` sətir 24-də də təkrarlanır |
| **AC-26** Build + testlər | ✅ | 0 warning / 0 error; 694/694 yaşıl; commit mesajı `feat: abune sistemi ve platforma admin api` |

**Yekun: 24 ✅ · 2 ❌ (AC-14, AC-16).** AC-2 və AC-5 hərfi deviasiya ilə, lakin funksional olaraq ödənilib və sənədləşdirilib.

---

## 3. Test Case matrisi

| TC | Nəticə | Örtən test |
|---|---|---|
| **TC-1** register → pending, login 403 | ✅ | `PlatformAdminApiTests.Registration_Creates_A_Pending_Shop_Whose_Owner_Cannot_Sign_In_Yet` |
| **TC-2** approve(1 ay) → login OK | ✅ | `.Approving_A_Pending_Shop_Lets_Its_Owner_Sign_In` |
| **TC-3** ExpiresAt keçmiş → istənilən API 403 | ✅ (kod adı deviasiyası, BUG-2) | `.An_Expired_Subscription_Blocks_Every_Authenticated_Call` — `/api/products`, `/api/customers`, `/api/sales`, `/api/auth/me` üzərində; tenant statusu bazada `Active` qalır |
| **TC-4** ödəniş → yenidən işləyir, keçmiş baza kimi götürülmür | ✅ | `.Recording_A_Payment_Reopens_An_Expired_Shop` (eyni token, yenidən login tələb olunmur) |
| **TC-5** Owner/Manager/Seller → 403, anonim → 401 | ✅ | `.An_Ordinary_Owner_Is_Forbidden_From_The_Whole_Admin_Surface`, `.Manager_And_Seller_Are_Also_Forbidden_From_The_Admin_Surface`, `.The_Admin_Surface_Is_Closed_To_Anonymous_Callers` |
| **TC-6** təkrar telefon → 409 | ✅ | `.Registering_An_Already_Used_Phone_Is_409`, `.Registration_Is_Refused_For_A_Phone_That_Already_Logs_In` |
| **TC-7** `ExpiresAt = null` → 200 | ✅ | `.A_Shop_Without_An_Expiry_Is_Never_Auto_Blocked`; BE#35 paketi bütövlükdə yaşıl |
| **TC-8** qalıq müddət itmir | ✅ | `.Payment_Adds_To_A_Live_Period_And_Restarts_A_Lapsed_One` (birinci qol); `SubscriptionPeriodTests.Extending_A_Live_Period_Adds_To_The_Remaining_Time` |
| **TC-9** keçmiş müddət baza kimi götürülmür + sətir sahələri | ✅ | eyni test (ikinci qol) + `.Recording_A_Payment_Reopens_An_Expired_Shop` (`Amount`, `PeriodMonths`, `Note`, `RecordedByAdminId`) |
| **TC-10** admin login, `/me`, `/stats` | ✅ | `.Platform_Admin_Can_Read_Its_Own_Profile_And_Carries_The_Platform_Role`, `.Platform_Admin_Passes_The_Tenant_Gate_But_Sees_No_Shop_Data`, `.Stats_Track_Shop_States_And_This_Months_Takings` (delta olaraq real saylara uyğunluq) |
| **TC-11** block → unblock, `ExpiresAt` dəyişmir, status filtri | ✅ | `.Block_And_Unblock_Do_Not_Touch_The_Subscription`; status filtri `.Tenant_List_Filters_By_Status_And_Search_And_Shows_The_Billing_Summary` |
| **TC-12** Blocked login mesajı konfiqurasiyadakı telefonla | ✅ | `Be36QaGapTests.Tc12_A_Blocked_Shop_Is_Refused_At_Login_With_The_Configured_Phone` (QA yazdı) |
| **TC-13** canlı sessiya block olunanda növbəti sorğu 403 | ✅ (kod adı deviasiyası) | `.Block_And_Unblock_Do_Not_Touch_The_Subscription` — keş yoxdur, dərhal 403. TC `Auth.TenantInactiveForbidden` gözləyir, faktiki `Auth.TenantBlockedForbidden` (şüurlu wire dəyişikliyi, `ERROR-CONTRACT.md` + `CHANGELOG.md`-də sənədləşib) |
| **TC-14** naməlum id → hər yerdə 404 | ✅ | `.Approve_Validates_Its_Input_And_Its_Target`, `.Payment_Validates_Amount_Period_And_Target` (POST + GET payments), `.Block_And_Unblock…` (block/unblock) |
| **TC-15** `amount` 0/-10, `months` 0/-1 → 400, heç nə yazılmır | ✅ | `.Payment_Validates_Amount_Period_And_Target` (0, -5, months 0) + `Be36QaGapTests.Tc15_A_Negative_Period_Is_Rejected_And_Writes_Nothing` (-1, üstəlik sətir yaranmadığı və `ExpiresAt` dəyişmədiyi) |
| **TC-16** ödəniş tarixçəsi mağazalar arası sızmır, azalan sıra | ✅ | `Be36QaGapTests.Tc16_Payment_History_Is_Per_Shop_And_Newest_First` (QA yazdı) |
| **TC-17** `collectedThisMonth` keçən ayı saymır | ✅ | `Be36QaGapTests.Tc17_This_Months_Takings_Exclude_A_Payment_From_Last_Month` (QA yazdı — keçən ay tarixli sətir birbaşa bazaya yazılır, rəqəm tərpənmir) |
| **TC-18** axtarış: ad / sahibkar / telefon, **fərqli registrlə** | ❌ **FAIL** | **BUG-1** — böyük hərfli `I` daşıyan termin heç nə tapmır. Bax §5 |
| **TC-19** admin mağaza yaradır → sahibkar dərhal login | ✅ | `.Admin_Created_Shop_Is_Active_At_Once_And_Its_Owner_Can_Sign_In` (OBS-3: «yalnız öz boş mağazasını görür» hissəsi 200 ilə yoxlanılır, boşluq iddia edilmir) |
| **TC-20** arxitektura testi süni pozuntuda qırılır | ✅ | **QA canlı təkrarladı** — bax §4 |
| **TC-21** register uğursuzluğunda yetim Tenant qalmır | ✅ | `Be36QaGapTests.Tc21_A_Failed_Owner_Creation_Leaves_No_Orphan_Tenant` (QA yazdı) |
| **TC-22** miqrasiyadan sonra köhnə quraşdırma işləyir | ✅ (OBS-4) | Hər inteqrasiya işində host bütün miqrasiyaları tətbiq edir; `.A_Shop_Without_An_Expiry_Is_Never_Auto_Blocked` demo mağazanın (`İlk Mağaza`, `ExpiresAt = null`) login + `/api/products` 200 aldığını təsdiqləyir. «Sətir sayları eyni qalır» hissəsi ayrıca iddia edilmir |

**Yekun: 21 ✅ · 1 ❌ (TC-18)**

---

## 4. Müstəqil təsdiqlər (QA-nın öz ölçmələri)

### 4.1 TC-20 / AC-23 — arxitektura testi həqiqətən qırılır

`src/Modules/MayaPro.WarehouseApi.Modules.Products/QaTemporaryBypassProbe.cs` faylı müvəqqəti yaradıldı (allow-list-dən kənar, kompilyasiya olunan `source.IgnoreQueryFilters()` çağırışı) və test işə salındı:

```
Failed IgnoreQueryFiltersArchitectureTests.No_Undeclared_Query_Filter_Bypass_Exists_In_The_Source
  Tenant filtri icazəsiz yerdə söndürülüb (IgnoreQueryFilters). Ya çağırışı silin, ya da
  əsaslandırma ilə allowlist-ə əlavə edin:
  src/Modules/MayaPro.WarehouseApi.Modules.Products/QaTemporaryBypassProbe.cs
Failed: 1, Passed: 2, Total: 3
```

Pozuntunun **faylı mesajda görünür**. Probe silindikdən sonra 3/3 yaşıl. ✅

### 4.2 «CI-də 0 fayl tapıb səssizcə yaşıl keçə bilərmi?» — XEYR

Skaner iki qatlı qorunur:

- `The_Scanner_Actually_Reads_The_Source_Tree` → `src/` altında **>100** `.cs` faylı tapılmasını və `FilesWithBypass()`-ın **boş olmamasını** tələb edir. Boş tarama səssiz yaşıl deyil, açıq qırmızıdır.
- `RepositoryRoot()` `MayaPro.WarehouseApi.sln`-ə qədər yuxarı qalxır və tapmasa `Assert.NotNull` ilə **partlayır** (repo-dan kənar published output-da işlədilsə də səssiz keçmir).

`bin/`/`obj/` istisna edilir, doc-comment-lər sayılmır (`//` və `*` ilə başlayan sətirlər atılır) — yəni saxta müsbət də yoxdur. ✅

### 4.3 Təhlükəsizlik — PlatformAdmin token ilə data sızması

| Endpoint | Nəticə |
|---|---|
| `GET /api/products` | 200, **boş massiv** (`Platform_Admin_Passes_The_Tenant_Gate_But_Sees_No_Shop_Data`) |
| `GET /api/products/{id}` (mövcud, yad mağazanın) | **404** — route deyil, filtr rədd edir |
| `GET /api/customers` | 200, **boş** |
| `GET /api/sales` | 200, **boş**, `total = 0` (`Be36QaGapTests.Platform_Admin_Reads_No_Sales_Either` — QA əlavə etdi) |
| `GET /api/admin/tenants/{A}/payments` | Yalnız A-nın sətirləri (`Tc16_…`) |

Səbəb strukturaldır: `PlatformTenantId` altında bir dənə də biznes sətri yazılmır, ona görə hər tenant-scoped sorğu **fail-closed** boş qayıdır. Sızma tapılmadı. ✅

### 4.4 Reqressiya (BE#35 və digər modullar)

- `TenantIsolationApiTests` — yaşıl (`Auth.TenantInactiveForbidden` → `Auth.TenantBlockedForbidden` wire dəyişikliyinə uyğun yenilənib).
- `TenantQueryFilterCoverageTests` — yaşıl, `tenancy` sxemi üçün qapalı allow-list ilə genişlənib.
- Products / Sales / Customers / Suppliers / Expenses / Reports / DayEnd / Exports / Auth / SharedKernel — hamısı yaşıl.
- Wire dəyişikliyi sənədləşdirilib: `docs/api/ERROR-CONTRACT.md` (sətir 46–49, 64) və `docs/changes/CHANGELOG.md` (sətir 9) — **hər ikisində açıq «Wire dəyişikliyi» qeydi var.** ✅

---

## 5. Tapılan buglar

### BUG-1 — Mağaza axtarışı Azərbaycan mədəniyyətində böyük `I` üçün sınır (Severity: **Medium**, funksional)

**Aid olduğu AC/TC:** AC-14 (cədvəl, 1-ci sətir: «`search` … case-insensitive, partial»), TC-18
**Fayl:** `src/Modules/MayaPro.WarehouseApi.Modules.Tenancy/Application/Admin/UseCases/GetTenants/GetTenantsHandler.cs:43`

**Təkrarlama addımları**

1. Platforma admini kimi mağaza yarat/qeydiyyatdan keçir, adı `QaAxtaris11b42255Magaza`, sahibkar `QaSahibkar11b42255Adi`.
2. `GET /api/admin/tenants?search=qaaxtaris11b42255` → **1 sətir** (tapılır).
3. `GET /api/admin/tenants?search=QAAXTARIS11B42255` → **0 sətir**.
4. `GET /api/admin/tenants?search=qasahibkar11b42255` → 1 sətir; `?search=QASAHIBKAR11B42255` → **0 sətir**.
5. Nəzarət: tərkibində `I` OLMAYAN termin hər registrdə işləyir — `MAGAZA`, `MAGAZa`, `mAGAZA`, `magaza`, `Magaza` → hamısı 1 sətir.

**QA-nın ölçdüyü matris**

```
qaaxtaris11b42255  => TAPILDI (1)
QAAXTARIS11B42255  => TAPILMADI (0)   <-- pozuntu
Magaza             => TAPILDI (1)
magaza             => TAPILDI (1)
MAGAZA             => TAPILDI (1)
MAGAZa             => TAPILDI (1)
mAGAZA             => TAPILDI (1)
qasahibkar11b42255 => TAPILDI (1)
QASAHIBKAR11B42255 => TAPILMADI (0)   <-- pozuntu
0000001 (telefon)  => TAPILDI (1)
```

**Gözlənilən:** hər üç sahə üzrə registrdən asılı olmayan (case-insensitive) qismən uyğunluq.
**Faktiki:** termində böyük `I` varsa nəticə həmişə boşdur — xəta yox, **səssiz boş siyahı**.

**Kök səbəb (ölçülüb, fərziyyə deyil)**

```
CurrentCulture=az-Latn-AZ | "QAAXTARIS".ToLower()=qaaxtarıs | ToLowerInvariant()=qaaxtaris | equal=False
```

`GetTenantsHandler` termini **C# tərəfdə** `query.Search.Trim().ToLower()` ilə kiçildir. Host `az-Latn-AZ` mədəniyyətində işlədiyi üçün `'I'` → `'ı'` (U+0131, nöqtəsiz i) çevrilir. Sütun tərəfi isə SQL Server-in `LOWER()`-i ilə DB kollasiyasına görə `'I'` → `'i'` verir. İki tərəf uyğunlaşmır, `LIKE` heç vaxt tutmur.

**Niyə vacibdir:** məhsulun hədəf bazarı məhz Azərbaycandır və mağaza/sahibkar adlarında böyük `I` çox yayılmışdır (`IŞIQ`, `IDEAL`, `Ismayıl`, `ISTANBUL`…). Admin böyük hərflə yazanda konsol «mağaza yoxdur» deyir.

**Tövsiyə olunan istiqamət (developer qərar verir):** hər iki tərəfi eyni qaydaya gətirmək — məs. `ToLowerInvariant()` istifadə etmək, yaxud `.ToLower()` çağırışlarını tamamilə çıxarıb `EF.Functions.Like` / kollasiyaya güvənmək (SQL Server kollasiyası onsuz da CI-dir; §4.1-dəki `MAGAZA` sətri bunu göstərir). Düzəlişlə birlikdə TC-18 reqressiya testi əlavə edilməlidir.

**Əlaqəli (BE#36 xaricində, pre-existing):** eyni nümunə `src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Application/UseCases/CreateExpenseType/CreateExpenseTypeHandler.cs:29-31`-də də var — `ISI` / `isi` kimi adlarda təkrar yoxlaması yanılda bilər. BE#36-nın qəbulunu bloklamır, ayrıca task-a layiqdir.

---

### BUG-2 — Abunə bitmə xəta kodu AC-16-dakı dondurulmuş sətirdən fərqlidir (Severity: **Medium**, kontrakt)

**Aid olduğu AC/TC:** AC-16, TC-3
**Fayl:** `src/MayaPro.WarehouseApi.Api/Middleware/TenantGateMiddleware.cs:57` (`SubscriptionExpiredCode`), `Modules.Auth/Domain/AuthErrors.cs`

**Təkrarlama:** müddəti keçmiş `Active` mağazanın tokeni ilə `GET /api/products`.

- **Gözlənilən (AC-16, hərfən):** `{ "code": "SubscriptionExpired", "message": "Abunəliyiniz bitib — əlaqə: …" }`
- **Faktiki:** `{ "code": "Auth.SubscriptionExpiredForbidden", "message": "Abunəliyiniz bitib — əlaqə: 0509999999" }`

**Qiymətləndirmə:** davranış (403, status dəyişmir, mesaj konfiqurasiyadan) tam düzgündür və seçilmiş ad layihənin `Auth.XxxForbidden` konvensiyasına uyğundur, `ERROR-CONTRACT.md`-də sənədləşib. Risk **frontend inteqrasiyasındadır**: AC-16 bu sətri «frontend bu koda görə xüsusi ekran göstərəcək» deyə donduraraq yazmışdı. Frontend agenti issue-dakı AC-ni oxuyub `code === "SubscriptionExpired"` yazsa, abunə ekranı heç vaxt açılmayacaq.

**Qərar tələb olunur (PM/orchestrator):** ya AC-16 rəsmi olaraq yenilənsin (`Auth.SubscriptionExpiredForbidden` kanonik qəbul edilsin), ya da kod AC-yə uyğunlaşdırılsın. QA öz-özünə seçmir — hər iki halda frontend taskının kontraktı bu qərarla eyni olmalıdır.

---

### BUG-3 — Admin body/response sahə adları AC-14-dən fərqlidir (Severity: **Medium**, kontrakt)

**Aid olduğu AC/TC:** AC-14 (3-cü və 6-cı sətir), AC-15, TC-15
**Fayllar:** `Application/Admin/UseCases/ApproveTenant/ApproveTenantCommand.cs`, `.../RecordPayment/RecordPaymentCommand.cs`, `.../CreateTenant/CreateTenantCommand.cs`, `Application/Contracts/TenancyDtos.cs`

| Yer | AC-14 tələbi | Faktiki |
|---|---|---|
| `POST /api/admin/tenants/{id}/approve` body | `periodMonths` | `months` |
| `POST /api/admin/tenants/{id}/payments` body | `periodMonths` | `months` |
| `POST /api/admin/tenants` body | müddət (ay) | `months` |
| `GET /api/admin/stats` cavabı | `collectedThisMonth` | `thisMonthCollected` |

**Təkrarlama:** `POST /api/admin/tenants/{id}/payments` body `{ "amount": 50, "periodMonths": 1 }` göndər.
**Gözlənilən:** 200, müddət 1 ay uzanır.
**Faktiki:** `periodMonths` bağlanmır, `Months = 0` qalır → **400** «Ay sayı 1 ilə 120 arasında olmalıdır».

**Qeyd:** cavab DTO-su (`SubscriptionPaymentDto`) `periodMonths` qaytarır, sorğu isə `months` qəbul edir — eyni anlayış üçün asimmetrik adlandırma, frontend üçün ayrıca çaşqınlıq mənbəyidir. `docs/api/API-OVERVIEW.md` faktiki (`months`) variantı sənədləşdirir, yəni sənəd–kod uyğunluğu var; uyğunsuzluq **sənəd ↔ AC** arasındadır. BUG-2 ilə eyni qərar tələb olunur.

---

## 6. İcra OLUNMAYAN yoxlamalar və səbəbləri

| Yoxlama | Vəziyyət | Səbəb |
|---|---|---|
| `dotnet run` ilə ayrıca qaldırılmış proses üzərində canlı curl smoke | **İcra olunmadı** | Prosesi fon rejimində mühit dəyişənləri ilə başlatmaq bu QA mühitində icazə tələb etdi və verilmədi. **Əvəzedici tam ekvivalentdir:** `WarehouseApiFactory` real hostu (bütün middleware, auth pipeline, Serilog, migration-lar) real SQL Server (`MayaProWarehouse_Test`) üzərində qaldırır və bütün ssenari faktiki HTTP sorğuları ilə icra olunur — register → 201 → login 403 → approve → login 200 → expiry → 403 → ödəniş → 200 zənciri daxil olmaqla. In-memory DB və ya mock istifadə edilməyib. Uydurma nəticə yazılmayıb |
| Qeydiyyatda paralel yarış (eyni telefonla eyni anda iki `register`) | **İcra olunmadı** | Developer tərəfindən açıq şəkildə qalıq risk kimi qəbul edilib və `docs/multi-tenancy.md` §4.1 (sətir 184) + §8-də Mərhələ 3-ə təxirə salınıb; AC siyahısında yoxdur. Determinist ölçmək üçün ayrıca yük aləti lazımdır |
| Rate-limit davranışı | **Tətbiq olunmur** | AC-25 hazırda rate-limit OLMADIĞINI sənədləşdirməyi tələb edir — bu ödənilib. Funksiya Mərhələ 3-dədir |
| PostgreSQL / fərqli DB kollasiyası üzərində BUG-1-in davranışı | **İcra olunmadı** | Layihə SQL Server üzərindədir; başqa provayder konfiqurasiya olunmayıb |
| Frontend e2e (abunə ekranı, admin konsolu UI) | **Tətbiq olunmur** | BE#36 backend taskıdır; `frontend/` folderinə toxunulmayıb |

---

## 7. Bloklamayan müşahidələr

- **OBS-1 (AC-4):** `PlatformAdminSeeder.SeedAsync` konfiqurasiya boş olanda **səssizcə** qayıdır. AC-4 «log warning» tələb edir. Production-da `PlatformAdmin__Password` təyin edilməsə, admin seed olunmur və bunun heç bir izi qalmır — deploy zamanı diaqnostikanı çətinləşdirir. Bir sətirlik `ILogger.LogWarning` kifayətdir.
- **OBS-2 (AC-12):** `SubscriptionPayment.RecordedByAdminId` `Guid?`-dir, AC `Guid` deyir. Kodda «yalnız tooling sətirləri üçün null» şərhi var; endpoint həmişə `ICurrentUser`-dan doldurur. Real risk yoxdur, sənəd ↔ AC uyğunsuzluğudur.
- **OBS-3 (TC-19):** `Admin_Created_Shop_Is_Active_At_Once_And_Its_Owner_Can_Sign_In` yeni sahibkarın `/api/products`-dan **200** aldığını yoxlayır, lakin siyahının **boş** olduğunu iddia etmir (TC-19 «yalnız öz boş mağazasını görür» deyir). Təcrid onsuz da `TenantIsolationApiTests` ilə örtülüdür; bir `Assert.Empty` sətri boşluğu bağlayardı.
- **OBS-4 (TC-22):** «Miqrasiyadan sonra sətir sayları dəyişmir» hissəsi ayrıca iddia edilmir. Miqrasiya `AddSubscriptionFields` yalnız sütun əlavə edir (back-fill yoxdur), ona görə risk nəzəri qalır; hər inteqrasiya işi miqrasiyanı real DB üzərində tətbiq edir.
- **Kiçik sənəd qeydi:** `IgnoreQueryFiltersArchitectureTests` XML şərhi testi «BE#36, AC-7» adlandırır; əslində AC-21/AC-22/AC-23-dür. `docs/multi-tenancy.md` §5.1.1 də «AC-7» yazır. Yalnız istinad nömrəsidir, davranışa təsiri yoxdur.

---

## 8. Yekun qərar

**QA FAILED → task `In Progress`-ə qaytarılır.**

| Səbəb | Bloklayıcı? |
|---|---|
| **BUG-1** — axtarış `az-Latn-AZ`-də böyük `I` üçün sınır (AC-14, TC-18) | **Bəli** — funksional pozuntu, hədəf bazarda gündəlik istifadə ssenarisi |
| **BUG-2** — `SubscriptionExpired` kod sətri AC-16-dan fərqlidir | Bəli (qərar tələb edir) — frontend kontraktı bundan asılıdır |
| **BUG-3** — `months` / `thisMonthCollected` adları AC-14-dən fərqlidir | Bəli (qərar tələb edir) — frontend kontraktı bundan asılıdır |

BUG-2 və BUG-3 üçün iki məqbul yol var: (a) kodu AC-yə uyğunlaşdırmaq, (b) AC-ni rəsmi olaraq yeniləyib frontend taskının kontraktını sənədə (`ERROR-CONTRACT.md`, `API-OVERVIEW.md`) bağlamaq. Hansı seçilirsə, **frontend taskı başlamazdan əvvəl** qərar verilməlidir.

Qalan bütün sahələr — təhlükəsizlik, təcrid, abunə riyaziyyatı, atomiklik, arxitektura qorunması, sənədləşdirmə — QA tərəfindən müstəqil təsdiqləndi və **qəbul edilir**.
