# API Overview

Bütün route-lar `/api/...`, JSON camelCase, tarixlər ISO 8601, pul decimal (JSON number). Wire dəyərləri (ödəniş növləri, rollar) dondurulub — bax [ADR-0006](../decisions/0006-frozen-wire-format.md). Xəta formatı: `docs/api/ERROR-CONTRACT.md`.

**Auth səviyyələri:** `anon` = açıq · `auth` = istənilən login olmuş rol · `O+M` = OwnerOrManager policy · `O` = OwnerOnly policy · `PA` = PlatformAdminOnly policy (platforma operatoru; adi Sahibkar da daxil olmaqla heç bir mağaza rolu keçmir). Rol çatmır → 403.

**Multi-tenancy (BE#35/BE#36).** Autentifikasiya olunmuş HƏR sorğu tenant qapısından keçir: token `tenantId` daşımırsa 401; mağaza tapılmır/təsdiq gözləyir/bloklanıb/abunə müddəti bitibsə 403 (kodlar `docs/api/ERROR-CONTRACT.md`-də). Yeganə istisna `PlatformAdmin` rolu ilə gələn tokendir — o, heç bir mağazaya aid deyil, ona görə qapıdan keçir, lakin mağaza datasını GÖRMÜR (query filter boş qaytarır). Detallar: `docs/multi-tenancy.md`.

## Endpoint-lər (62)

### Auth (`/api/auth`, `/api/employees`)
| Verb | Route | Auth | Qeyd |
|---|---|---|---|
| POST | `/api/auth/login` | anon | `{phone, password}` → `{token, user}` |
| POST | `/api/auth/register` | anon | `{storeName, ownerName, phone, password}` → 201 `{tenantId, storeName, status, message}` |
| GET | `/api/auth/me` | auth | Cari istifadəçi |
| GET | `/api/employees` | auth | İşçi siyahısı (`monthlySalary` daxil) |
| PUT | `/api/employees/{id}/salary` | O | `{monthlySalary}` → yenilənmiş işçi sətri |
| GET | `/api/employees/salary-summary?month=` | O+M | Hər işçi üzrə aylıq maaş hesabı |
| POST | `/api/employees/{id}/salary-entries` | O+M | `{type, amount, note?, month?}` → 201 |
| GET | `/api/employees/{id}/salary-entries?month=` | auth | Ayın maaş sətirləri (ən yenisi əvvəldə) |
| DELETE | `/api/employees/{id}/salary-entries/{entryId}` | O | Maaş sətrini silir |

**Mağaza qeydiyyatı (BE#36).** `POST /api/auth/register` anonimdir və mağazanı `PendingApproval` statusunda yaradır + ilk `Sahibkar` istifadəçisini verir — **token qaytarmır**, çünki mağaza hələ girə bilmir. Telefon **qlobal** (bütün mağazalar üzrə) unikal olmalıdır: təkrar → 409 `Tenancy.PhoneAlreadyExists`. 400 halları: boş mağaza/sahibkar adı (>200 simvol), boş telefon (>30 simvol), şifrə < 6 simvol. Mağaza + istifadəçi tək transaction-da yazılır. **Rate-limit YOXDUR** — Mərhələ 3-ə qalıb (`docs/multi-tenancy.md` §8).

Qeydiyyatdan sonrakı login cavabları: `PendingApproval` → 403 `Auth.TenantPendingApprovalForbidden` «Hesabınız təsdiq gözləyir»; `Blocked` → 403 `Auth.TenantBlockedForbidden` «Abunəliyiniz bitib — əlaqə: {admin telefonu}»; abunə müddəti keçib → 403 `SubscriptionExpired` (eyni mesaj; prefikssiz ad qəsdəndir — `ERROR-CONTRACT.md`).

**Telefon formatı (BE#46).** Telefon qəbul edən hər endpoint girişi **kanonik** `994XXXXXXXXX` formasına salır və o formada saxlayır: `POST`/`PUT /api/customers`, `POST`/`PUT /api/suppliers`, `PUT /api/settings`, `POST /api/auth/register`, `POST /api/admin/tenants`. Qəbul edilən yazılışlar `994…` (12 rəqəm) və `0…` (10 rəqəm) ilə onların `+`/boşluq/`-`/`(`/`)` variantlarıdır; başqa hər hal → 400 «Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)». **Cavab DTO-ları da kanonik dəyəri qaytarır** (`customerDto.phone`, `supplierDto.phone`, `userDto.phone`, `settingsDto.phone`, tenant sətirləri) — sahə adları/tipləri dəyişməyib, yalnız məzmun bir formaya gəldi. `POST /api/auth/login` girişi eyni qayda ilə normallaşdırıb axtarır, ona görə köhnə formatda yazılmış nömrə ilə giriş işləyir; oxuna bilməyən nömrə format xətası yox, neytral «Telefon və ya şifrə yanlışdır» alır. Qaydanın tam mətni: `docs/business/BUSINESS-RULES.md` → «Telefon nömrəsi qaydaları».

**Maaş sistemi (BE#28).** `GET /api/employees` cavabına additiv `monthlySalary` sahəsi əlavə olundu (təyin edilməyibsə `0`, heç vaxt null); mövcud sahələr dəyişməyib.

`type` dondurulmuş wire dəyəridir: `"payment"` (maaş/avans ödənişi — kassadan real pul çıxır) və ya `"deduction"` (yemək/yol/cərimə — yalnız işçinin hesabından tutulur, kassaya TOXUNMUR). `month` `yyyy-MM` formatındadır və göndərilmirsə cari Bakı ayı (ADR-0005) götürülür. Sətrin `date` sahəsi (pulun çıxdığı an) və `month` sahəsi (hansı ayın hesabına) AYRIDIR: keçən ayın maaşını bu gün ödəmək `date = bu gün`, `month = keçən ay` deməkdir — gün sonu/dashboard `date`-ə, maaş xülasəsi `month`-a baxır.

`salary-summary` hər işçi üçün bir sətir qaytarır (`userId, fullName, role, monthlySalary, paidTotal, deductionTotal, remaining`); sətri olmayan işçi də `0/0/monthlySalary` ilə görünür. `remaining = monthlySalary − paidTotal − deductionTotal` MƏNFİ ola bilər — «artıq ödənilib» deməkdir, xəta deyil.

Kassa təsiri: `payment` sətirləri gün sonu bağlanışında mövcud `expenses` rəqəminin İÇİNƏ əlavə olunur və dashboard-un `todayExpenses`/`expectedCash` sahələrinə düşür. `deduction` heç birinə düşmür. **BE#33:** həmin `payment` cəmi indi ayrıca da görünür — `GET /api/reports/summary`-nin cavabına (istənilən `period` üçün) additiv `salaryExpenses` sahəsi, `POST /api/closings` və `GET /api/closings*`-in cavabına additiv `ClosingDto.salaryExpenses` sahəsi əlavə olundu (`expenses = generalExpenses + productExpenses + salaryExpenses`; `Expenses`/`ExpectedCash`-in özü DƏYİŞMƏYİB, sadəcə artıq mövcud rəqəmin bir hissəsi ayrıca göstərilir).

400 halları: `Salary.InvalidType` («Maaş əməliyyatının növü yanlışdır»), `Salary.InvalidMonth` («Ay formatı yanlışdır (yyyy-MM)»), «Məbləğ sıfırdan böyük olmalıdır», «Qeyd 500 simvoldan uzun ola bilməz», «Maaş mənfi ola bilməz». 404 halları: `Auth.UserNotFound`, `Salary.EntryNotFound` (sətir yoxdursa VƏ YA route-dakı işçiyə aid deyilsə — cross-user sızma yoxdur).

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

### PlatformAdmin (`/api/admin`) — hamısı `PA` (BE#36)
| Verb | Route | Qeyd |
|---|---|---|
| GET | `/api/admin/tenants?status&search` | Mağaza siyahısı + billing xülasəsi |
| POST | `/api/admin/tenants` | Admin özü mağaza yaradır → dərhal `Active`, 201 |
| POST | `/api/admin/tenants/{id}/approve` | `{periodMonths}` → `ExpiresAt = now + N ay`, status `Active` |
| POST | `/api/admin/tenants/{id}/block` · `/unblock` | Status keçidi — abunə müddətinə TOXUNMUR |
| POST | `/api/admin/tenants/{id}/payments` | `{amount, periodMonths, note?}` → ödəniş yazılır + müddət uzanır |
| GET | `/api/admin/tenants/{id}/payments` | Ödəniş tarixçəsi (ən yenisi əvvəldə) |
| GET | `/api/admin/stats` | `{activeCount, pendingCount, blockedCount, expiredCount, collectedThisMonth}` |

`GET /api/admin/tenants` hər sətirdə: `id, name, ownerName, phone, status, expiresAt, monthlyFee, isExpired, lastPaymentAt, lastPaymentAmount, totalPaid`. `status` filtri `TenantStatus` adıdır (`PendingApproval` \| `Active` \| `Blocked`, case-insensitive) — naməlum dəyər 400; `search` mağaza adı / sahibkar adı / telefon üzrə case-insensitive `contains`. Registrdən asılılıq **DB collation-ı** ilə həll olunur (`LIKE`, C# tərəfdə `ToLower()` YOXDUR) — BE#40: `az-Latn-AZ` mədəniyyətində `'I'.ToLower()` `'ı'` (U+0131) verirdi və SQL `LOWER()`-in `'i'`-si ilə heç vaxt üst-üstə düşmürdü. `%`, `_`, `[` simvolları literal kimi axtarılır (escape olunur).

`POST /api/admin/tenants` body-si: `{storeName, ownerName, phone, password, periodMonths?, monthlyFee?}`. `periodMonths` verilməsə mağaza **müddətsiz** `Active` olur. Telefon qaydası qeydiyyatla eynidir (409).

**Abunə uzatma qaydası:** ödənişdə `ExpiresAt = max(now, mövcud ExpiresAt ?? now) + N ay` (vaxtından əvvəl ödəyən qalan günlərini itirmir; gecikən müddəti indidən başlayır). Təsdiqdə isə `ExpiresAt = now + N ay` (təmiz başlanğıc). `ExpiresAt = null` = **müddətsiz**, heç vaxt bloklanmır.

Validasiya: `periodMonths` 1–120 arası (kənar → 400), `amount` > 0 və ≤ 1 000 000 (→ 400), `note` ≤ 500 simvol, mövcud olmayan mağaza → 404 `Tenancy.TenantNotFound`.

`expiredCount` = statusu `Active`, amma `ExpiresAt` keçmiş mağazalar (avto-blok statusu dəyişmir). `collectedThisMonth` cari **Bakı** təqvim ayının ödəniş cəmidir (ADR-0005).

**Sahə adları (BE#42):** müddət sahəsi hər yerdə `periodMonths`-dur — sorğuda da, `GET /api/admin/tenants/{id}/payments` cavabında da. Endpoint-lər heç bir istehlakçıya çıxmadan düzəldiyi üçün köhnə `months` / `thisMonthCollected` adları üçün alias YOXDUR.

### Activity, Health
`GET /api/activity?take&skip` (auth) · `GET /health` (anon)

## DTO referansı

Dəqiq DTO sahələri üçün: modulun `Application/Contracts/*Dto.cs` faylları; frontend tipləri `docs/index.ts` (kontraktın frontend tərəfi); test wire assert-ləri `tests/.../WireFormatApiTests.cs`.

## Last Updated

2026-08-22 — BE#46: telefon qəbul edən bütün endpoint-lər girişi kanonik `994XXXXXXXXX` formasına salır və cavab DTO-ları da o formada qaytarır (sahə adları/tipləri dəyişməyib); yeni 400 mesajı «Telefon nömrəsi düzgün formatda deyil (məs: 050 123 45 67)»; login istənilən yazılışla işləyir.

2026-08-16 — BE#40/41/42 (BE#36 QA düzəlişləri): admin axtarışı registrdən asılı deyil (`az-Latn-AZ` `'I'` problemi); abunə kodu `SubscriptionExpired`; sorğu sahəsi `months` → `periodMonths`, stats sahəsi `thisMonthCollected` → `collectedThisMonth` (alias yoxdur).

2026-08-16 — BE#36: `POST /api/auth/register` (anon) və `/api/admin/*` platforma konsolu (8 endpoint, `PA` policy); login-in 403 cavabları statusa görə ayrıldı (`TenantPendingApproval` / `TenantBlocked` / `SubscriptionExpired`); abunə müddəti keçən mağaza istənilən authenticated sorğuda 403 alır.

2026-08-01 — BE#28: işçi maaş sistemi — `PUT /api/employees/{id}/salary` (O), `POST`/`GET`/`DELETE /api/employees/{id}/salary-entries` və `GET /api/employees/salary-summary`; `GET /api/employees` cavabına additiv `monthlySalary`. Maaş ödənişləri gün sonu `expenses` və dashboard `todayExpenses`/`expectedCash` rəqəmlərinə daxil oldu; tutulmalar kassaya toxunmur.

2026-07-30 — BE#15: POST/PUT `/api/sales` üzərinə `paidAmount`/`paidVia`, cavab DTO-larına `paidAmount`/`remainingAmount`/`paidVia`; «Nisyə satış üçün müştəri seçilməlidir» mesajı «Qalıq borc üçün müştəri seçilməlidir» ilə əvəz olundu.

2026-07-30 — BE#12: `POST /api/products/{id}/generate-barcode` (O+M, SDK barkodu) və `POST /api/exports/products/labels.pdf` (barkod/QR etiket vərəqi).

2026-07-27 — BE#4: `GET/POST /api/expense-types` (idarə olunan xərc növləri), `Expense.category` sərbəst string oldu, `source` (general/product) sahəsi + `GET /api/expenses?source` filtri, summary-ə `generalExpenses`/`productExpenses`; təchizatçı ilkin borcu + `GET /api/suppliers/{id}/history`.

## Related Code

- `src/Modules/*/Endpoints/*.cs` (bütün route-lar)
- `src/MayaPro.WarehouseApi.Api/Extensions/AuthenticationExtensions.cs` (policy tərifləri)
