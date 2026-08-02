# QA Report — BE#27: Səhifə KPI endpoint-ləri (products/sales/debts-kpi)

**Tarix:** 2026-08-02
**QA Agent:** qa-tester
**Test edilən PR:** https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/30 (branch `task/BE27-sehife-kpi-endpointleri`)
**Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/27

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Acceptance Criteria (ümumi, AC-G1..G6) | 6 — 5 ✅ / 1 ❌ (AC-G4) |
| PM test case-ləri (PK-U*, SK-U*, DK-U*, PK-I*, SK-I*, DK-I1/I3/I4) | Hamısı ✅ Pass |
| Yaradılan bug sayı | 1 (BE#31) |
| **Yekun qərar** | **FAIL → In Progress** |

## 1. Build & Avtomatik testlər

| Yoxlama | Nəticə |
|---|---|
| `dotnet build MayaPro.WarehouseApi.sln` | ✅ Pass — 0 Warning, 0 Error |
| `dotnet test MayaPro.WarehouseApi.sln` (tam suite) | ✅ Pass — **523/523** test yaşıl, 0 uğursuz |

Modullar üzrə paylanma: SharedKernel 36, Reports 47, DayEnd 4, Suppliers 12, Sales 50, Exports 46, Products 74, Expenses 54, Customers 20, Auth 4, IntegrationTests 176 → cəmi 523. Developer/senior-backend raportu ilə üst-üstə düşür.

## 2. PM AC/Test case-lərinin qarşılanması (kod baxışı + unit test icrası)

### products-kpi
| # | Ssenari | Nəticə |
|---|---|---|
| PK-U1..U6 | Happy path, boş anbar, out-of-stock, soldUnits (sərbəst satış daxil), purchasedUnits (yeni+adjust), mənfi adjust xaric | ✅ Pass (`ProductsKpiCalculatorTests.cs`, gözlənilən rəqəmlərlə tam uyğun) |
| AC2 (from/to yalnız dövr sahələrinə təsir) | ✅ Pass (`PK_AC2_...` testi) |
| PK-I1..I4 | Happy path, dövr xaric, tərs aralıq→400, auth yoxdur→401 | ✅ Pass (`ReportsApiTests.cs`) |

### sales-kpi
| # | Ssenari | Nəticə |
|---|---|---|
| SK-U1..U4 | Happy path, unknown-profit xaric, boş dövr, toxunulmamış ödəniş tipi 0-la görünür | ✅ Pass (`SalesKpiCalculatorTests.cs`) |
| SK-I1..I3 | Happy path, tərs aralıq→400, auth yoxdur→401 | ✅ Pass |

### debts-kpi
| # | Ssenari | Nəticə |
|---|---|---|
| DK-U1..U5 | Happy path, borclu yoxdur, periodNewDebt (qismən ödənişli nisyə), periodCollected, tie-break (adla) | ✅ Pass (`DebtsKpiCalculatorTests.cs`) |
| DK-I1, DK-I3, DK-I4 | Happy path, tərs aralıq→400, auth yoxdur→401 | ✅ Pass |
| DK-I2 (CustomerDebtAdjustment / köçürülmüş açılış qalığı edge-case) | ⚠️ Ayrıca test yazılmayıb, PM-in özü "sənədləşdirilməli" deyə qeyd etmişdi — kod baxışında `oldestDebtDays`-in yalnız `GetOutstandingSalesAsync()`-dən (Nisyə satışlar) hesablandığı, açılış qalıqlarının bu siyahıda olmadığı təsdiqləndi (bilinən məhdudiyyət, blokerdeyil) |

### Ümumi (AC-G1..G6)
- AC-G1 (eyni auth qrupu) ✅, AC-G2 (Calculator+Handler ayrımı) ✅, AC-G3 (yeni DTO-lar, camelCase) ✅ (əl ilə yoxlanılan JSON cavablarında təsdiqləndi), AC-G5 (build+test yaşıl) ✅, AC-G6 (yeni kontrakt unit testləri: `ProductsModuleContractTests.cs`, `CustomersModuleContractTests.cs`) ✅.
- **AC-G4 (yanlış tarix formatı → 400) ❌ FAIL** — aşağıda ətraflı.

## 3. Əl ilə (manual) HTTP testi — lokal işə salınmış API

Backend lokal olaraq işə salındı (`dotnet run`, port 5299, dev DB), demo istifadəçi ilə (`0501112233` / `demo123`) login olunub JWT token alındı, sonra hər üç endpoint müxtəlif ssenarilərlə sınandı:

| Ssenari | Gözlənilən | Faktiki | Nəticə |
|---|---|---|---|
| Auth-suz sorğu (`products-kpi`) | 401 | 401 | ✅ Pass |
| Boş from/to (bütün 3 endpoint) | 200, bütün tarixçə | 200, məntiqli cəmi rəqəmlər (məs. products-kpi: productCount=15, soldUnits=21, purchasedUnits=791) | ✅ Pass |
| Tərs aralıq (`from=2026-08-10&to=2026-08-01`, bütün 3 endpoint) | 400 `Reports.InvalidDateRange` | 400 `Reports.InvalidDateRange` | ✅ Pass |
| Real dar aralıq (bugün=2026-08-02) — fəaliyyətdən əvvəl | 200, dövr sahələri 0 | 200, `soldUnits=0/purchasedUnits=0` (products-kpi), `salesCount=0` (sales-kpi), `periodNewDebt=0/periodCollected=0` (debts-kpi); snapshot sahələri (productCount, totalStockUnits, totalOutstanding) dəyişmədi | ✅ Pass |
| Real ssenari — məhsul yarat (qty=20, cost=5, sale=10), 3 ədəd nağd satış, +5 adjust-stock, müştəri yarat, 1 ədəd nisyə satış (10), 4 AZN ödəniş | Bütün delta-lar məntiqli | `products-kpi`: soldUnits +4 (3+1), purchasedUnits +25 (20 opening+5 adjust), totalStockUnits +21 (20-3+5-1), totalCostValue +105 (21×5), totalSaleValue +210 (21×10) — riyazi olaraq tam uyğun. `sales-kpi`: salesCount +2, totalRevenue +40, totalProfit +20, byPayment Nağd +30/+15, Nisyə +10/+5 — uyğun. `debts-kpi`: totalOutstanding +6 (10 yeni borc − 4 ödəniş), debtorCount +1, periodNewDebt=10, periodCollected=4 — uyğun | ✅ Pass |
| **Yanlış tarix formatı** (`from=not-a-date&to=2026-08-02`, bütün 3 endpoint) | 400 Bad Request (AC-G4) | **500 Internal Server Error** (`{"code":"Server.Error","message":"Gözlənilməz xəta baş verdi"}`) | ❌ **FAIL** — bax Bug BE#31 |

## 4. Tapılan bug-lar

### BE#31 — [BUG][BE#27] products/sales/debts-kpi: yanlış tarix formatı 400 yox, 500 qaytarır (AC-G4)
- **Issue:** https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/31
- **Prioritet:** Medium
- **Qısa təsvir:** Üç yeni KPI endpoint `from`/`to`-nu birbaşa minimal-API `DateOnly?` parametri kimi bind edir; parse edilə bilməyən string göndərildikdə ASP.NET-in binding xətası `GlobalExceptionHandler` tərəfindən tutulub 500-ə çevrilir, halbuki AC-G4 açıq şəkildə 400 tələb edir. Layihədə artıq işlək pattern mövcuddur (`ExportsEndpoints.TryParseOptionalDate`, `string?` qəbul edib explicit parse/400) — eyni yanaşma bu 3 endpoint-ə də tətbiq edilməlidir.

## 5. Yekun qiymətləndirmə

- Build: ✅ Pass
- Avtomatik testlər: ✅ 523/523 Pass
- PM-in bütün unit/integration test case-ləri (PK-U*, SK-U*, DK-U*, PK-I*, SK-I*, DK-I1/I3/I4): ✅ Pass
- Əl ilə HTTP testi (auth, boş aralıq, tərs aralıq, real ssenari, dövrün snapshot sahələrinə təsirsizliyi): ✅ Pass
- Əl ilə HTTP testi (yanlış tarix formatı → gözlənilən 400): ❌ **FAIL** (AC-G4 pozulur, bax BE#31)

**Ümumi nəticə: FAIL** — 1 bug (BE#31) aşkarlandı, AC-G4-ü tam qarşılamır. Qalan bütün funksionallıq (əsas 3 KPI endpoint-in düzgün hesablanması, auth, tərs aralıq validasiyası, snapshot vs dövr sahələrinin ayrılması) tam işləkdir. Task geri `In Progress`-ə qaytarılır, BE#31 developer tərəfindən düzəldilməlidir.
