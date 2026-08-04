# QA Report (RETEST) — BE#27: Səhifə KPI endpoint-ləri (products/sales/debts-kpi)

**Tarix:** 2026-08-04
**QA Agent:** qa-tester
**Test edilən PR:** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/30 (branch `task/BE27-sehife-kpi-endpointleri`, açıqdır, main-ə merge olunmayıb)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/27
**Test edilən commit:** `ffc0368` (merge commit — PR #32/BE#31 fix-i BE#27 branch-ına daxil edir)
**Bu retest-in səbəbi:** İlk QA dövründə ([`docs/qa/BE-27-qa-report.md`](./BE-27-qa-report.md)) bütün AC/test case-lər ✅ PASS idi, YALNIZ **AC-G4** (yanlış/parse edilə bilməyən `from`/`to` göndərildikdə 400 gözlənilir) ❌ FAIL idi (real sorğu 500 qaytarırdı). Bunun üçün bug **BE#31** yaradıldı, düzəldildi (commit `8341e41` + refactor `ead8a6b`) və PR #32 ilə bu branch-a merge edildi (`ffc0368`). Bu retest yalnız AC-G4-ün indi düzgün davranmasını və regressiya olmadığını təsdiqləmək üçündür.

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Fokus | AC-G4 (yanlış tarix formatı → 400) yenidən yoxlanışı + tam regressiya |
| AC-G4 nəticəsi | ✅ **PASS** (əvvəllər ❌ FAIL idi) |
| Build | ✅ Pass — 0 Warning, 0 Error |
| Tam test suite | ✅ Pass — **533/533** (əvvəlki 523 + 10 yeni: 7 `OptionalDateQueryTests` + 3 `ReportsApiTests` HTTP-səviyyəli AC-G4 testi) |
| Regressiya (əvvəllər PASS olan bütün AC/test case-lər) | ✅ Heç bir pozulma tapılmadı |
| Yeni bug | Yoxdur |
| **Yekun qərar** | **PASS → Done** |

## 1. Kod baxışı — BE#31 düzəlişi

Əvvəlki bug-un kök səbəbi: 3 yeni KPI endpoint `from`/`to`-nu birbaşa `DateOnly?` minimal-API parametri kimi bind edirdi; parse edilə bilməyən string göndərildikdə ASP.NET-in binding xətası `GlobalExceptionHandler` tərəfindən tutulub 500-ə çevrilirdi.

Düzəliş kod baxışında təsdiqləndi:

- Yeni paylaşılan helper: `src/MayaPro.WarehouseApi.SharedKernel/Application/OptionalDateQuery.cs` — `TryParse(string? raw, out DateOnly? date, out string? error)`: boş/whitespace → keçərli (unbounded), parse olunan → `DateOnly`, parse olunmayan → `false` + Azərbaycanca xəta mesajı (exception atmır).
- `ReportsEndpoints.cs`-də `products-kpi`, `sales-kpi`, `debts-kpi` endpoint-lərinin hər üçü indi `from`/`to`-nu **raw `string?`** kimi qəbul edir (`DateOnly?` yox), `OptionalDateQuery.TryParse` ilə explicit yoxlayır, uğursuz olduqda `Results.BadRequest(new { code = "Reports.InvalidFrom"/"InvalidTo", message = ... })` qaytarır — mövcud `{ code, message }` error kontraktına tam uyğun, `ExportsEndpoints`-dəki eyni pattern-lə üst-üstə düşür (kod dublikasiyası refactor `ead8a6b` ilə aradan qaldırılıb).
- Tərs aralıq (`from > to`) validasiyası handler səviyyəsində qalıb (`Reports.InvalidDateRange`) — bu düzəlişdən təsirlənməyib, ayrıca yoxlanılıb (aşağıda).

Kod baxışı: düzəliş düzgün, minimal, mövcud pattern-lərlə (ExportsEndpoints) uyğundur.

## 2. Build & Avtomatik test suite (tam solution)

| Yoxlama | Nəticə |
|---|---|
| `dotnet build MayaPro.WarehouseApi.sln` | ✅ Pass — 0 Warning, 0 Error |
| `dotnet test MayaPro.WarehouseApi.sln` (tam suite) | ✅ Pass — **533/533** test yaşıl, 0 uğursuz |

Modullar üzrə paylanma: SharedKernel **43** (əvvəl 36, +7 `OptionalDateQueryTests`), DayEnd 4, Reports 47, Suppliers 12, Exports 46, Expenses 54, Customers 20, Auth 4, Sales 50, Products 74, IntegrationTests **179** (əvvəl 176, +3 `ProductsKpi_PK_I5`/`SalesKpi_SK_I4`/`DebtsKpi_DK_I5_Unparsable_From_Returns_400_Not_500`) → cəmi 533. Riyaziyyat dəqiq üst-üstə düşür: 523 (əvvəlki) + 7 + 3 = 533. **Heç bir mövcud test pozulmayıb (regressiya yoxdur).**

## 3. AC-G4 — HTTP-səviyyəli təsdiq (real sorğular, TestServer üzərindən)

Backend-i lokal `dotnet run` ilə işə salıb xarici socket vasitəsilə (`curl`) əl ilə sınamağa cəhd edildi, lakin bu sessiyanın sandbox mühiti şəbəkə/proses alətlərinə (`curl`, `tasklist`, s.) icazə vermədi (yalnız `dotnet build`/`dotnet test`, `git`, fayl oxuma icazəlidir). Bunun əvəzinə **filtered `dotnet test`** icra edildi — bu, `WebApplicationFactory`/`TestServer` üzərindən **real HTTP sorğuları** (tam routing + model binding + exception handling + JSON serialization pipeline-ı ilə) göndərir, əvvəlki QA dövründəki əl ilə `curl`/Python skripti testi ilə **funksional olaraq eynidir** (fərq yalnız TCP soketinin əvəzinə in-process transport-dur):

```
dotnet test tests/MayaPro.WarehouseApi.IntegrationTests \
  --filter "FullyQualifiedName~Unparsable_From_Returns_400_Not_500" \
  --logger "console;verbosity=detailed"
```

Nəticə (log-dan, hər üç endpoint üçün demo istifadəçi `0501112233`/`demo123` ilə login olunub JWT alınıb, sonra `from=not-a-date&to=2026-08-02` ilə çağırılıb):

| Endpoint | Sorğu | Gözlənilən | Faktiki | Nəticə |
|---|---|---|---|---|
| `GET /api/reports/products-kpi` | `?from=not-a-date&to=2026-08-02` | 400 | **400** (`Reports.InvalidFrom`) | ✅ PASS |
| `GET /api/reports/sales-kpi` | `?from=not-a-date&to=2026-08-02` | 400 | **400** (`Reports.InvalidFrom`) | ✅ PASS |
| `GET /api/reports/debts-kpi` | `?from=not-a-date&to=2026-08-02` | 400 | **400** (`Reports.InvalidFrom`) | ✅ PASS |

```
Passed MayaPro.WarehouseApi.IntegrationTests.ReportsApiTests.DebtsKpi_DK_I5_Unparsable_From_Returns_400_Not_500 [1 s]
Passed MayaPro.WarehouseApi.IntegrationTests.ReportsApiTests.ProductsKpi_PK_I5_Unparsable_From_Returns_400_Not_500 [122 ms]
Passed MayaPro.WarehouseApi.IntegrationTests.ReportsApiTests.SalesKpi_SK_I4_Unparsable_From_Returns_400_Not_500 [106 ms]
Total tests: 3, Passed: 3
```

Loglarda HTTP status kodu aydın görünür: `Setting HTTP status code 400` → `HTTP GET ... responded 400` — **500 deyil**. Əvvəlki dövrdə tam eyni ssenari `500 Internal Server Error` (`Server.Error`) qaytarırdı — indi bu tam düzəlib.

Əlavə olaraq şəbəkə vasitəsilə birbaşa əl ilə (real `dotnet run` + `curl`) sınaq bu sessiyada mümkün olmadı (sandbox icazə məhdudiyyəti) — bu, yuxarıda izah edildiyi kimi ekvivalent TestServer-əsaslı HTTP testi ilə kompensasiya olundu. Əgər tam mühitdə (sandbox-suz) təkrar-təsdiq tələb olunarsa, bu qeyd olunmalıdır, lakin test metodologiyası (real HTTP client + tam pipeline) baxımından fərq yoxdur.

### Unit səviyyəsində əlavə təsdiq — `OptionalDateQueryTests`

`tests/MayaPro.WarehouseApi.SharedKernel.Tests/OptionalDateQueryTests.cs` (7 test):
- `null`/`""`/`"   "` → keçərli, `date=null`, `error=null` (unbounded) ✅
- `"2026-08-02"` → düzgün parse ✅
- `"not-a-date"`, `"2026-13-40"`, `"08/02/2026 garbage"` → **exception atmır**, `ok=false` + boş olmayan `error` mesajı ✅

Bütün 7 test yaşıldır.

## 4. Regressiya — əvvəllər PASS olan AC/test case-lər

Task təsvirinə uyğun olaraq, dərin təkrar-test aparılmadı (build+test suite tam yaşıl olduğu üçün kifayət hesab edildi), amma aşağıdakılar əlavə təsdiqləndi:

- **PK-\*, SK-\*, DK-\* (unit + integration)**: dəyişməyib, hamısı test suite-də yaşıl (`ProductsKpiCalculatorTests`, `SalesKpiCalculatorTests`, `DebtsKpiCalculatorTests`, `ReportsApiTests` — köhnə 176 test hələ də mövcud və yaşıl).
- **AC-G1 (auth qrupu), AC-G2 (Calculator/Handler ayrımı), AC-G3 (DTO/camelCase), AC-G6 (kontrakt testləri)**: kod dəyişməyib (yalnız `from`/`to` parametr tipi `DateOnly?` → `string?` və parse yeri dəyişib), bu AC-lərə təsiri yoxdur.
- **AC-G5 (build+test yaşıl)**: ✅ təsdiqləndi (yuxarıda, 533/533).
- **Tərs aralıq validasiyası (`from > to` → 400 `Reports.InvalidDateRange`)**: kod baxışında handler-də dəyişməyib (yalnız binding mərhələsi dəyişib, parse edilmiş `DateOnly` handler-ə ötürülür), mövcud `ReportsApiTests`-dəki `..._I3`/`SK_I2`/`DK_I3` testləri (tərs aralıq) hələ də mövcuddur və yaşıldır.

Heç bir regressiya tapılmadı.

## 5. Yekun qiymətləndirmə

- **AC-G4 (yanlış tarix formatı → 400 gözlənilir): ✅ PASS** (əvvəllər ❌ FAIL, BE#31 ilə düzəldilib, indi həm unit (`OptionalDateQueryTests`), həm HTTP-səviyyəli integration testlə (`PK_I5`/`SK_I4`/`DK_I5`) təsdiqlənir).
- Build: ✅ Pass, 0 Warning, 0 Error.
- Tam test suite: ✅ **533/533** Pass, 0 Fail (523 əvvəlki + 10 yeni BE#31 testi).
- Regressiya: ✅ Yoxdur — bütün əvvəllər PASS olan AC/test case-lər (PK-\*, SK-\*, DK-\*, AC-G1/G2/G3/G5/G6) hələ də yaşıldır.
- Yeni bug: **Yoxdur**.

**Ümumi nəticə: PASS.** BE#27 üzrə bütün Acceptance Criteria (AC-G1..G6, o cümlədən əvvəllər uğursuz olan AC-G4) və PM-in bütün test case-ləri indi qarşılanır. Task **Done**-a keçirilə bilər. PR #30 (main-ə merge) üçün əlavə maneə görünmür.
