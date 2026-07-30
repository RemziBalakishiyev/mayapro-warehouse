# QA Report — BE-13: Excel import (şablon + iki mərhələli preview/commit, products)

**Tarix:** 2026-07-30
**QA Agent:** qa-tester
**Test edilən:** Issue https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/13, PR https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/17, branch `task/BE-13-excel-import`, commit `49c258b` (HEAD — senior backend review refactor-u daxil edir, `00b1348` üzərində).
**Mühit:** Lokal, Windows, .NET 8 SDK (dotnet SDK-nın host-ladığı runtime), tam solution üzərində `dotnet build` + `dotnet test` (standart `bin/Debug` çıxış qovluğu, əvvəlki sessiyalardan kilid problemi olmadı).

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 13 (AC-1…AC-13) |
| ✅ Pass | 13/13 |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Ümumi Test Case (TC-1…TC-17) | 17 |
| ✅ Pass | 17/17 |
| ❌ Fail | 0 |
| Yaradılan bug sayı | 0 |
| QA tərəfindən əlavə edilən yeni test sayı | 0 (səbəb: aşağıya bax — bütün tələb olunan sərhəd halları PR-in öz test dəstində artıq örtülüb) |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test: `dotnet test` (tam solution) → **405/405 keçdi**, 0 uğursuz, 0 skip.
Bölgü: `SharedKernel.Tests` 30, `Modules.Customers.Tests` 6, `Modules.Sales.Tests` 20, `Modules.DayEnd.Tests` 4, `Modules.Reports.Tests` 17, `Modules.Suppliers.Tests` 12, `Modules.Expenses.Tests` 52, `Modules.Exports.Tests` 38, `Modules.Products.Tests` 71, `Modules.Auth.Tests` 4, `IntegrationTests` 151 (real HTTP + `WebApplicationFactory`/SQL Server üzərində).

## Nəzərdən keçirilən kod

- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/ProductImportTemplate.cs` — dondurulmuş sütun/başlıq kontraktı (Exports və Products modulları arasında paylaşılır).
- `src/MayaPro.WarehouseApi.SharedKernel/Application/ResultExtensions.cs` — `...TokenNotFound`/`...TokenExpired` → 410 mapping-i, `NotFound`-dan əvvəl yoxlanılır.
- `src/Modules/.../Modules.Exports/Application/UseCases/ExportProductsTemplate/ExportProductsTemplateHandler.cs` — şablon workbook-u.
- `src/Modules/.../Modules.Products/Endpoints/ImportsEndpoints.cs` — preview/commit endpoint-ləri, multipart-ın müdafiəli oxunması.
- `src/Modules/.../Modules.Products/Application/UseCases/PreviewProductsImport/PreviewProductsImportHandler.cs` — parse + təsnifat, DB-yə yazmır.
- `src/Modules/.../Modules.Products/Application/Imports/ImportRowParser.cs` — sətir səviyyəli validasiya (Ad/qiymət/miqdar/xüsusiyyət/uzunluq, onluq vergül/nöqtə dəstəyi).
- `src/Modules/.../Modules.Products/Application/UseCases/CommitProductsImport/CommitProductsImportHandler.cs` — tək transaksiyada tətbiq, aqreqat activity qeydi, barkod münaqişəsi yoxlaması.
- `src/Modules/.../Modules.Products/Infrastructure/ImportTokenCache.cs` — atomik `Claim` (TryRemove), TTL, `Restore`, expired-sweep.

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC-1 | Şablon endpoint — 200, düzgün Content-Type, 2 vərəq, qalın başlıq + 2 nümunə sətir, Azərbaycanca qaydalar, istənilən rol | ✅ PASS | `ExportsEndpoints.cs:23` — `RequireAuthorization()` (rol məhdudiyyəti yoxdur). `ExportProductsTemplateHandlerTests` — başlıqlar `ProductImportTemplate.Headers`-lə bitə-bitə eynidir, `Cell(1,1).Style.Font.Bold`, 3 sətir (başlıq+2 nümunə), 2-ci vərəqdə "məcburidir", "Ölçü: M; Rəng: Qara", "1000", "yenilənməsi" mətnləri. İnteqrasiya: `ExportsApiTests.Products_Template_Returns_Two_Sheet_Workbook_For_Any_Role` — **Seller** rolu ilə real HTTP 200, `Content-Disposition: ...mallar-sablon.xlsx`, workbook 2 vərəqli, başlıqlar kontraktla eyni. |
| AC-2 | Preview 200, `{importToken, rows, summary}` formatı, DB-yə heç nə yazılmır | ✅ PASS | `ImportPreviewResponse`/`ImportRowResult`/`ImportSummary` DTO-ları spesifikasiyanın `{rowNumber,status,data,error?}` / `{creates,updates,errors,newCategories}` formasına bitə-bitə uyğundur. `PreviewProductsImportHandlerTests.Classifies_Create_Update_And_Error_Rows_Without_Writing_To_The_Database` — `db.Products.CountAsync()==1` (yalnız əvvəldən mövcud olan), `db.Categories.CountAsync()==0`. İnteqrasiya: `ImportsApiTests.Preview_Classifies_A_Mixed_File_And_Writes_Nothing_To_The_Database` — real HTTP, preview-dən əvvəl/sonra `/api/products` sayı eynidir. |
| AC-3 | Sətir təsnifatı: create/update/error qarışıq | ✅ PASS | Eyni testlər + `ImportsApiTests` — 3 sətirlik fayl (yeni barkod / mövcud barkod / mənfi qiymət) → `rows[0]="create"`, `rows[1]="update"`, `rows[2]="error"`, `error="Satış qiyməti mənfi"`, `summary.creates=1/updates=1/errors=1`. |
| AC-4 | Yeni kateqoriya aşkarlanması | ✅ PASS | `Flags_A_Category_That_Does_Not_Exist_Yet_As_New_On_A_Create_Row` + `ImportsApiTests.Preview_Flags_A_Category_That_Does_Not_Exist_Yet` — `summary.NewCategories` siyahısında adı görünür, sətir `create`. |
| AC-5 | Boş fayl → 400 `Imports.EmptyFile` | ✅ PASS | `Header_Only_File_Returns_EmptyFile_Error`, `Null_File_Returns_EmptyFile_Error` + inteqrasiya `Preview_With_Empty_File_Returns_400_EmptyFile`, `Preview_Without_A_File_Part_Returns_400_EmptyFile` (fayl hissəsi ümumiyyətlə göndərilmədikdə də eyni kod). |
| AC-6 | >1000 sətir → 400 `Imports.TooManyRows` | ✅ PASS | `More_Than_1000_Data_Rows_Returns_TooManyRows_Error` (1001 sətir) + `Exactly_1000_Data_Rows_Is_Still_Accepted` (sərhəd — 1000 hələ qəbul olunur) + inteqrasiya `Preview_With_More_Than_1000_Rows_Returns_400_TooManyRows`. Mesajda limit ədədi (1000) var. |
| AC-7 | Yanlış şablon → 400 `Imports.InvalidTemplate`, dəqiq mesaj | ✅ PASS | `Mismatched_Headers_Return_InvalidTemplate_Error`, `Not_An_Excel_File_Returns_InvalidTemplate_Error` (oxunmayan/xlsx-olmayan fayl da eyni nəticəyə düşür) + inteqrasiya `Preview_With_Wrong_Headers_Returns_400_InvalidTemplate` — mesaj `"Şablona uyğun deyil — şablonu endirib istifadə et"` dəqiq assert olunub. |
| AC-8 | Rol icazəsi — Seller → 403 body-siz | ✅ PASS | İnteqrasiya `Preview_Is_Forbidden_For_A_Seller` — real HTTP 403 (framework səviyyəsində `OwnerOrManager` policy). |
| AC-9 | importToken 10 dəq TTL — köhnəlmiş token → 410 `Imports.TokenExpired` | ✅ PASS | Unit `ImportTokenCacheTests.Token_Past_Its_Ttl_Is_Expired_Not_NotFound` (10 dəq 1 san sonra → Expired, 9 dəq 59 san-da hələ `Found` — sərhəd hər iki tərəfdən yoxlanılıb, `FakeDateProvider` ilə real gözləmə yoxdur) + `CommitProductsImportHandlerTests.Expired_Token_Returns_TokenExpired` (saat 11 dəq irəli çəkilir, `ImportErrors.TokenExpired`, DB-yə heç nə yazılmayıb). |
| AC-10 | Commit happy path — create+update, tək transaksiya, aqreqat activity qeydi | ✅ PASS | `CommitProductsImportHandlerTests.Commits_New_Category_New_Product_And_Updated_Product_In_One_Transaction_With_Activity_Log` — yeni kateqoriya yaranıb, yeni mal `RealCostPerUnit`=alış qiyməti/`InitialQuantity`=idxal miqdarı, mövcud mal ad/qiymət/stok yenilənib (`InitialQuantity` isə **dəyişməz** qalıb — düzgün, yalnız yaradılışda təyin olunur), activity mesajı dəqiq `"Excel import: 1 yeni, 1 yenilənmə"`. İnteqrasiya: `Commit_Applies_Only_Valid_Rows_Creates_A_Category_And_Logs_One_Activity_Entry` — real HTTP+DB+activity feed. |
| AC-11 | Commit yalnız valid sətirləri yazır, error sətirlər tam atlanır | ✅ PASS | `Commit_Skips_Error_Rows_And_Only_Applies_Valid_Ones` — 2 sətirdən yalnız sağlam olan DB-yə yazılıb (`db.Products.CountAsync()==1`). İnteqrasiya testində "Commit xətalı mal" məhsullar siyahısında **yoxdur** (`Assert.DoesNotContain`). |
| AC-12 | Naməlum token → 410 `Imports.TokenNotFound` | ✅ PASS | `Unknown_Token_Returns_TokenNotFound_As_410`, `Missing_Token_Returns_TokenNotFound` (boş/`null` token) + inteqrasiya `Commit_With_An_Unknown_Token_Returns_410` — mesaj dəqiq assert olunub. |
| AC-13 | Rol icazəsi — Seller commit → 403 | ✅ PASS | İnteqrasiya `Commit_Is_Forbidden_For_A_Seller` — real HTTP 403. |

## Test case nəticələri (issue-dakı TC-1…TC-17)

| TC | Ssenari | Nəticə | Faktiki test |
|---|---|---|---|
| TC-1 | Şablon — istənilən rol, 200, 2 vərəq | ✅ PASS | `ExportsApiTests.Products_Template_Returns_Two_Sheet_Workbook_For_Any_Role` (Seller ilə), `ExportProductsTemplateHandlerTests` (2×) |
| TC-2 | Preview — qarışıq 5 sətirlik fayl, düzgün təsnifat, DB dəyişmir | ✅ PASS | `ImportsApiTests.Preview_Classifies_A_Mixed_File_And_Writes_Nothing_To_The_Database` |
| TC-3 | Yeni kateqoriya aşkarlanması | ✅ PASS | `ImportsApiTests.Preview_Flags_A_Category_That_Does_Not_Exist_Yet` |
| TC-4 | Mövcud barkoda uyğun sətir → update | ✅ PASS | `PreviewProductsImportHandlerTests.Classifies_Create_Update_And_Error_Rows...` (rows[1]) |
| TC-5 | Ad boşdur → error | ✅ PASS | `Empty_Name_Is_An_Error_Row` |
| TC-6 | Alış qiyməti rəqəm deyil → error | ✅ PASS | `NonNumeric_Purchase_Price_Is_An_Error_Row` |
| TC-7 | Satış qiyməti mənfi → error | ✅ PASS | `Negative_Sale_Price_Is_An_Error_Row` |
| TC-8 | Boş fayl → `Imports.EmptyFile` | ✅ PASS | `Preview_With_Empty_File_Returns_400_EmptyFile` |
| TC-9 | 1001 sətir → `Imports.TooManyRows` | ✅ PASS | `Preview_With_More_Than_1000_Rows_Returns_400_TooManyRows` |
| TC-10 | Yanlış başlıq → `Imports.InvalidTemplate` | ✅ PASS | `Preview_With_Wrong_Headers_Returns_400_InvalidTemplate` |
| TC-11 | Seller preview → 403 | ✅ PASS | `Preview_Is_Forbidden_For_A_Seller` |
| TC-12 | Commit happy path — yeni kateqoriya+mallar, activity qeydi | ✅ PASS | `Commit_Applies_Only_Valid_Rows_Creates_A_Category_And_Logs_One_Activity_Entry` |
| TC-13 | Valid+error qarışıq token ilə commit — yalnız validlər tətbiq olunur | ✅ PASS | eyni test (`"Commit xətalı mal"` `DoesNotContain`), unit `Commit_Skips_Error_Rows_And_Only_Applies_Valid_Ones` |
| TC-14 | Uydurma token → 410 `TokenNotFound` | ✅ PASS | `Commit_With_An_Unknown_Token_Returns_410` |
| TC-15 | Token 10 dəqiqədən sonra → 410 `TokenExpired` | ✅ PASS | Unit `CommitProductsImportHandlerTests.Expired_Token_Returns_TokenExpired` (real vaxt gözləmədən saat irəli çəkilir — TTL yoxlaması funksional səviyyədə sübut olunur; HTTP səviyyəsində eyni məntiq `Committing_The_Same_Token_Twice...` ilə TokenNotFound qolu üçün örtülüb) |
| TC-16 | Seller commit → 403 | ✅ PASS | `Commit_Is_Forbidden_For_A_Seller` |
| TC-17 | Eyni token iki dəfə commit — ikincisi 410 | ✅ PASS | `Committing_The_Same_Token_Twice_Fails_The_Second_Time_With_410` (inteqrasiya) + unit `Committing_The_Same_Token_Twice_Fails_The_Second_Time` (DB-də bir dəfə tətbiq olunduğu təsdiqlənir) |

## Sərhəd hallarının əlavə yoxlanışı (tapşırıqda spesifik tələb olunan)

Aşağıdakı bütün hallar üçün ayrıca, spesifik test tapıldı (heç biri yalnız kod baxışı ilə "sənədləşdirilmirdi"):

- **>1000 sətir** — `More_Than_1000_Data_Rows_Returns_TooManyRows_Error` (1001) + `Exactly_1000_Data_Rows_Is_Still_Accepted` (sərhəd, 1000 hələ keçir).
- **Boş fayl** — `Header_Only_File_Returns_EmptyFile_Error`, `Null_File_Returns_EmptyFile_Error`, inteqrasiya `Preview_Without_A_File_Part_Returns_400_EmptyFile`.
- **Yanlış başlıq** — `Mismatched_Headers_Return_InvalidTemplate_Error`, `Not_An_Excel_File_Returns_InvalidTemplate_Error`.
- **Köhnəlmiş token 410** — `Token_Past_Its_Ttl_Is_Expired_Not_NotFound` (unit, `ImportTokenCache`) + `Expired_Token_Returns_TokenExpired` (handler səviyyəsində, DB-yə yazılmadığı da yoxlanır).
- **Yad istifadəçinin tokeni** — `Another_Users_Token_Cannot_Be_Committed_And_Stays_Usable_By_Its_Owner`: yad istifadəçi `TokenNotFound` (410) alır (token-in mövcudluğu sızmır), sonra sahib özü hələ də commit edə bilir (token istehlak olunmayıb).
- **Preview-in DB-yə yazmaması** — `Classifies_Create_Update_And_Error_Rows_Without_Writing_To_The_Database` + inteqrasiya (əvvəl/sonra məhsul sayı).
- **Commit-in yalnız valid sətirləri yazması** — `Commit_Skips_Error_Rows_And_Only_Applies_Valid_Ones` + inteqrasiya `DoesNotContain`.
- **Barkod üzrə update** — `Commits_New_Category_New_Product_And_Updated_Product_In_One_Transaction_With_Activity_Log` (mövcud mal barkoda görə tapılıb yenilənir, `InitialQuantity` toxunulmaz qalır), üstəlik `Update_Row_Whose_Product_Was_Deleted_Between_Preview_And_Commit_Is_Skipped` və `A_Barcode_Taken_Between_Preview_And_Commit_Aborts_The_Whole_Import` (preview-commit arası vəziyyət dəyişikliyi halları).
- **Transaction rollback** — `A_Failed_Transaction_Leaves_The_Token_Claimable_So_The_User_Can_Retry`: `ThrowingUnitOfWork` ilə transaksiya heç başlamır, `InvalidOperationException` atılır, amma token geri qaytarılır (`Restore`) və istifadəçi yenidən cəhd edə bilir, DB-də heç nə tətbiq olunmayıb.
- **Activity log mesajı** — hər iki happy-path testində `"Excel import: N yeni, M yenilənmə"` dəqiq mətn assert olunur (0 dəyərləri daxil, `Update_Row_Whose_Product_Was_Deleted...` testində `"...1 yeni, 0 yenilənmə"`).
- **Avtomatik kateqoriya yaradılması** — preview tərəfində `Flags_A_Category_That_Does_Not_Exist_Yet_As_New_On_A_Create_Row`, commit tərəfində `AddNewCategoriesAsync` — `db.Categories.CountAsync(c => c.Name == "Aksesuar") == 1` ilə təsdiqlənir.
- **Rol/auth — OwnerOrManager olmayan istifadəçi 403** — `Preview_Is_Forbidden_For_A_Seller`, `Commit_Is_Forbidden_For_A_Seller` (hər ikisi real HTTP, framework policy səviyyəsində, body-siz 403).

Bundan əlavə, PR-in öz test dəstində tələb olunandan da artıq sərhədlər örtülüb: fayl ölçüsü limiti (5 MB, oxunmadan əvvəl), onluq vergül/nöqtə qarışıqlığı (`"12,5"` vs `"12.5"`), sətir/sahə uzunluq limitləri (ad 200, xüsusiyyət 15 ədəd) preview səviyyəsində error kimi qaytarılır (commit zamanı DB-truncation/500 riski əvvəlcədən kəsilir), fayl daxilində təkrarlanan barkod (həm yeni, həm mövcud üçün) error kimi işarələnir, endirilmiş şablonun elə özünün preview tərəfindən qəbul edildiyi uçdan-uca test (`A_File_Built_On_The_Downloaded_Template_Is_Accepted_By_Preview`), və qeyri-multipart body-nin 500 əvəzinə 415/400 ilə rədd edilməsi.

## Tapılan buglar

Heç bir bug tapılmadı. Kod baxışı (bütün handler/endpoint/cache/parser sinifləri sətir-sətir oxundu) və tam solution-un icrası (`dotnet build` 0 xəta, `dotnet test` 405/405 yaşıl) heç bir uyğunsuzluq göstərmədi:

- HTTP status/kod xəritələnməsi (`ResultExtensions.StatusCodeFor`) `...TokenNotFound`/`...TokenExpired` xüsusi hallarını ümumi `NotFound`-dan əvvəl yoxlayır — 410 düzgün seçilir, `SharedKernel.Tests/ResultExtensionsTests.cs`-də bütün suffiks matrisası (o cümlədən `Imports.TokenExpired`/`Imports.TokenNotFound`) test olunub.
- Response DTO-ları (`ImportPreviewResponse`/`ImportRowResult`/`ImportSummary`) issue-dakı `{importToken, rows:[{rowNumber,status,data,error?}], summary:{creates,updates,errors,newCategories}}` formasına sözbəsöz uyğundur.
- Şablon başlıqları (`ProductImportTemplate`) tək mənbədən (SharedKernel) həm Exports-un yazdığı, həm Products-un oxuduğu tərəfdə istifadə olunur — drift riski yoxdur, uçdan-uca test bunu təsdiqləyir.
- Tapşırıqda spesifik istənilən bütün sərhəd halları (yuxarıdakı bölmə) ayrıca, adlı testlərlə örtülüb — QA tərəfindən yenidən yazmağa ehtiyac olmadı.

**Nəticə olaraq bu sessiyada QA tərəfindən yeni test faylı əlavə edilmədi** — yoxlanılan hər AC/TC və tapşırıqda spesifik sadalanan hər sərhəd halı üçün artıq PR-in özündə (dev + senior review refactor-u zamanı) adlı, məqsədyönlü, PASS vəziyyətində test mövcud idi. Kod da dəyişdirilmədi (QA-nın icazəsi yalnız test/report yazmaqdır).

## İcra olunan test əmrləri

```bash
git -C ".../backend" status
# On branch task/BE-13-excel-import, up to date with origin/task/BE-13-excel-import, clean

git -C ".../backend" log --oneline -3
# 49c258b refactor(BE#13): senior backend review duzelisleri
# 00b1348 feat: excel import iki merheleli
# 480cef1 Merge pull request #16 from .../task/BE-12-barcode-label-pdf

dotnet build
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test --no-build
# MayaPro.WarehouseApi.Modules.Customers.Tests    6/6
# MayaPro.WarehouseApi.Modules.Sales.Tests        20/20
# MayaPro.WarehouseApi.Modules.DayEnd.Tests       4/4
# MayaPro.WarehouseApi.Modules.Reports.Tests      17/17
# MayaPro.WarehouseApi.SharedKernel.Tests         30/30
# MayaPro.WarehouseApi.Modules.Suppliers.Tests    12/12
# MayaPro.WarehouseApi.Modules.Expenses.Tests     52/52
# MayaPro.WarehouseApi.Modules.Exports.Tests      38/38
# MayaPro.WarehouseApi.Modules.Products.Tests     71/71
# MayaPro.WarehouseApi.Modules.Auth.Tests         4/4
# MayaPro.WarehouseApi.IntegrationTests           151/151
# TOTAL: 405/405 passed, 0 failed, 0 skipped
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; branch `task/BE-13-excel-import` QA-nı problemsiz keçdi.
- Bug tapılmadı — backend taskı **Done** statusuna keçirilə bilər.
- Gələcək üçün qeyd (bloklayıcı deyil): PR-in özündə qeyd olunduğu kimi, commit cavabı hazırda boş gövdə (200) qaytarır; frontend "N yeni, M yenilənmə" göstərmək istəsə, cavabda say sahələrinin olması faydalı olardı (AC tələb etmir, scope-dan kənar).
