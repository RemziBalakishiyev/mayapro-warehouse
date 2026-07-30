# QA Report — BE-12: Barkod/QR generasiyası və etiket PDF çapı (Exports)

**Tarix:** 2026-07-30
**QA Agent:** qa-tester
**Test edilən:** Issue https://github.com/RemziBalakishiyev/mayapro-warehouse/issues/12, PR https://github.com/RemziBalakishiyev/mayapro-warehouse/pull/16, branch `task/BE-12-barcode-label-pdf`, commit `60fa45c` (HEAD — bu sessiyada əlavə olunan QA testlərini ehtiva edir; senior-review commiti `fa7306b` üzərində).
**Mühit:** Lokal, Windows, .NET 8 SDK (dotnet SDK 9.0.306 host), SQL Server (localhost, inteqrasiya test DB-si) — `dotnet build` / `dotnet test` bütün solution üzərində. Standart `bin/Debug` çıxışı əvvəlki sessiyalardan (`bin-be12/`, `bin-review/`, `bin-sr/`, `bin-qa/`) kilidli/işğal olunmuş ola biləcəyi üçün `-p:BaseOutputPath=bin-qa2/` alternativ çıxış qovluğu ilə build/test icra olundu. Bu bug sayılmır, mühit qeydidir (əvvəlki BE-9 QA sessiyasında qeyd olunan konvensiyaya uyğun).

## Xülasə

| Göstərici | Dəyər |
|---|---|
| Ümumi AC | 8 (AC1–AC8) |
| ✅ Pass | 8/8 |
| ❌ Fail | 0 |
| ⚠️ Blocked | 0 |
| Yaradılan bug sayı | 0 |
| QA tərəfindən əlavə edilən yeni test sayı | 15 (10 × `LabelSheetLayoutSpecTests` + 5 × `ExportProductLabelsPdfHandlerTests` əlavələri) |
| **Yekun qərar** | **PASS → Done** |

Build: `dotnet build -p:BaseOutputPath=bin-qa2/` → **Build succeeded, 0 Warning(s), 0 Error(s).**
Test (QA testləri əlavə olunmazdan əvvəl, mövcud PR vəziyyəti): `dotnet test -p:BaseOutputPath=bin-qa2/` → **309/309 keçdi**, 0 uğursuz.
Test (QA testləri əlavə olunduqdan sonra): `dotnet test -p:BaseOutputPath=bin-qa2/` → **324/324 keçdi**, 0 uğursuz, 0 skip (15 yeni QA testi daxil).

## Acceptance Criteria nəticələri

| AC | Təsvir | Nəticə | Qeyd |
|---|---|---|---|
| AC1 | `POST /api/products/{id}/generate-barcode` (OwnerOrManager) — barkodu boş olan mala unikal `"SDK"+7 rəqəm` barkod verir, cavabda yeni barkod var | ✅ PASS | `BarcodeGenerator.cs` — `"SDK" + Random 7 rəqəm`. `GenerateBarcodeHandler.cs` unikallığı unique-index üzərində retry (5 cəhd, 3 problu candidate) ilə təmin edir. Unit: `GenerateBarcodeHandlerTests.Assigns_Unique_SDK_Barcode_To_Product_Without_One`, `Gives_Every_Product_A_Distinct_Barcode` (50 mal, hamısı fərqli), `Retries_With_A_New_Code_When_The_Unique_Index_Rejects_The_Save`. İnteqrasiya: `ProductsApiTests.Generate_Barcode_Assigns_Unique_SDK_Code_To_Barcodeless_Product` — real HTTP, `^SDK[0-9]{7}$`, DB-də persist olunduğu təsdiqlənir. Endpoint `RequireAuthorization("OwnerOrManager")` ilə qorunur — `Seller_Cannot_Generate_Barcode_Returns_403` (403). |
| AC2 | Barkodu olan mala `generate-barcode` → 409 "Malın artıq barkodu var" | ✅ PASS | `ProductErrors.BarcodeAlreadyExists` kodu `AlreadyExists` ilə bitdiyi üçün paylaşılan Result→HTTP konvensiyasına görə 409-a map olunur. Unit: `GenerateBarcodeHandlerTests.Returns_BarcodeAlreadyExists_When_Product_Already_Has_A_Barcode` (barkod dəyişməz qalır). İnteqrasiya: `ProductsApiTests.Generate_Barcode_Returns_409_When_Product_Already_Has_One` — real HTTP 409, `error.Code == "Products.BarcodeAlreadyExists"`, mesaj dəqiq. |
| AC3 | `POST /api/exports/products/labels.pdf` (auth) → 200, body `%PDF` ilə başlayır, `Content-Disposition: attachment; filename="etiketler-{tarix}.pdf"` | ✅ PASS | `ExportProductLabelsPdfHandler.cs:113-117` — `$"etiketler-{today:yyyy-MM-dd}.pdf"`. Unit: `Produces_A_Pdf_Named_After_The_Business_Date` — `FixedDateProvider(2026-07-30)` ilə tam fayl adı `"etiketler-2026-07-30.pdf"` dəqiq assert olunur (tarix formatı gerçəkdən yoxlanılır, sadəcə prefiks yox). İnteqrasiya: `ExportsApiTests.Labels_Pdf_Returns_Pdf_With_Magic_Bytes_For_Barcoded_Products` — real HTTP 200, `ContentType == "application/pdf"`, `ContentDisposition.DispositionType` `"attachment"` ehtiva edir, fayl adı `"etiketler-"` ilə başlayır, body `%PDF` ilə başlayır. Auth: `Labels_Pdf_Requires_Authentication` — anonim sorğu 401. Endpoint qrupu `RequireAuthorization()` (rol məhdudiyyəti yoxdur — hər autentifikasiya olunmuş rol) — kod baxışı ilə təsdiqləndi. |
| AC4 | Barkodu olmayan mal siyahıda gəlsə → 400, mesajda mal ADLARI göstərilir | ✅ PASS | `ExportProductLabelsPdfHandler.cs:87-105` — hər barkodsuz malın adı `noBarcodeNames`-ə toplanır, `Distinct()` + `", "` ilə birləşdirilir. Unit: `Rejects_Products_Without_A_Barcode_And_Names_Them` (bir mal), `Names_Every_Barcodeless_Product_Once` — **iki fərqli** barkodsuz mal (təkrarlarla birlikdə) → mesaj `"Bu malların barkodu yoxdur: Birinci, İkinci"` — çoxluq (adlar, tək ad yox) faktiki doğrulanıb. İnteqrasiya: `Labels_Pdf_Returns_400_When_A_Product_Has_No_Barcode` — 400, `Exports.ProductsWithoutBarcode`, mesajda mal adı var. |
| AC5 | Cəmi 500-dən çox etiket → 400 | ✅ PASS | `ExportProductLabelsPdfHandler.cs:66-69` — say `long`-a cəmlənir (overflow qorumalı), `> 500` isə `TooManyLabels`. Unit: `Rejects_More_Than_500_Labels_In_Total` (300+201), `Accepts_Exactly_500_Labels` (sərhəd — 500 keçir), `Rejects_Counts_That_Would_Overflow_The_Total` (`int.MaxValue` × 2 — 500 ilə sərhədlənir, `OverflowException` yox). QA əlavə: `LabelSheetLayoutSpecTests.Sheet_Wide_Label_Cap_Is_500` — `MaxLabels` sabitinin literal dəyərinin özü reflection ilə 500-ə bərabər olduğu bağlanır (refaktor zamanı sürüşmə mühafizəsi). İnteqrasiya: `Labels_Pdf_Returns_400_When_Total_Count_Exceeds_500` — 501 → 400, `Exports.TooManyLabels`. |
| AC6 | A4, 3×8 grid, ~63×34mm etiket, kəsim boşluğu; hər etikettə mal adı (1-2 sətir truncate), qalın satış qiyməti "12.50 ₼", altda Code128 barkod (altında rəqəm) | ✅ PASS (QA testləri ilə örtük genişləndirildi) | Kod baxışı: `Columns=3`, `Rows=8`, `LabelWidthMm=63f`, `LabelHeightMm=34f`, `GapMm=2f` — səhifə marjinləri A4-ün öz (integer-rounded) ölçüsündən hesablanır ki, grid həmişə səhifəyə tam sığsın. `ComposeLabel` — ad (`ClampLines(2)`, `FontSize(6.5)`), qiymət (`.Bold().FontSize(9)`), kod şəkli (`Height(15mm)`), altında `label.Barcode` mətni (`FontSize(6)`) — tam AC6 sırası ilə. **Bu AC PR-in orijinal test dəstində yalnız kodun özü ilə "sənədləşdirilirdi" — heç bir test grid ölçülərini, qiymət formatını və ya ad kəsilməsini müstəqil doğrulamırdı** (private sabitlər/metodlar, ictimai seam yoxdur). QA bunun üçün `LabelSheetLayoutSpecTests.cs` (reflection əsaslı, 10 test) əlavə etdi: `Grid_Is_Three_Columns_By_Eight_Rows_Of_63x34mm_Labels_With_A_Cut_Gap` (Columns/Rows/LabelWidthMm/LabelHeightMm/GapMm sabitlərini birbaşa oxuyur), `Price_Renders_As_Two_Decimals_Followed_By_The_Manat_Sign` (4 case: `12.5→"12.50 ₼"`, `0→"0.00 ₼"`, `999.995→"1,000.00 ₼"` yarım-sent sərhəddə düzgün yuvarlaqlaşma, `7→"7.00 ₼"`), `Price_Format_Does_Not_Follow_The_Servers_Current_Culture` (thread culture `de-DE`-yə (vergüllü onluq ayırıcı) dəyişdirilir, çıxış yenə `"12.50 ₼"` — `FormatPrice`-ın "culture-independent" iddiası real sınaqdan keçirilib), `Long_Product_Name_Is_Truncated_With_An_Ellipsis` / `Short_Product_Name_Is_Printed_Unchanged` / `Name_At_Exactly_The_Cap_Is_Not_Truncated` (40 simvol sərhədi, `"..."` sonluğu). Barkodun altında rəqəm göstərilməsi kod baxışı ilə təsdiqləndi (`ComposeLabel` sətir 211, `label.Barcode` mətni) — vizual PDF-render nəticəsi ictimai seam olmadığı üçün mətn-səviyyəli assertlə örtülə bilmədi, lakin `LabelCodeImageRendererTests` kod şəklinin özünün skan olunan (decode olan) doğru dəyəri daşıdığını artıq sübut edir. |
| AC7 | body-də optional `"type": "barcode" \| "qr"` — QR seçiləndə barkod yerinə QR, içində barkod dəyəri | ✅ PASS (QA testi ilə əlaqə gücləndirildi) | `LabelCodeImageRendererTests.Barcode_Image_Decodes_Back_To_The_Value_As_Code128` / `Qr_Image_Decodes_Back_To_The_Value_As_Qr` — hər iki renderer funksiyası ayrı-ayrılıqda ZXing ilə **decode olunaraq** doğru dəyəri və doğru formatı (`CODE_128` / `QR_CODE`) qaytardığı sübut olunub (əsl scan-oluna-bilənlik testi, sadəcə byte assert deyil). Handler səviyyəsində `useQr = Type == "qr"` bayrağının həqiqətən `RenderQrCode`-a çatdığı `Produces_A_Pdf_For_Qr_Labels_Too` ilə "uğurla PDF yaranır" səviyyəsində örtülmüşdü, lakin **`Type` bayrağının faktiki renderer seçiminə təsir etdiyini** (yəni sükutla nəzərə alınmadığını) sübut edən heç bir test yox idi. QA əlavə etdi: `Qr_And_Barcode_Requests_Produce_Different_Documents_For_The_Same_Input` — eyni giriş (1 mal, 1 ədəd) `Type: null` və `Type: "qr"` ilə iki fərqli sənəd yaradır, `Assert.NotEqual(barcodeResult.Content, qrResult.Content)` — iki sənəd byte-səviyyəsində fərqlidir, deməli bayrağın özü render nəticəsinə həqiqətən təsir edir. |
| AC8 | `count` qədər eyni etiket ardıcıl düzülür (10 → 10 eyni etiket) | ✅ PASS (QA testləri ilə örtük genişləndirildi) | `ComposeGrid` — `labels.Chunk(Columns)` üzərindən iterasiya, `labels` siyahısı `foreach (LabelItemRequest item in requested) for (i=0;i<item.Count;i++) labels.Add(...)` ilə — sorğu sırası qorunur. Orijinal PR testləri (`Reuses_One_Code_Image_For_Every_Copy_Of_A_Barcode`, `Accepts_Exactly_500_Labels`) yalnız uğurlu render/ölçü sərhədini yoxlayırdı, **say artdıqca sənədə həqiqətən daha çox etiket əlavə olunduğunu** və **sorğu sırasının sənəddə əks olunduğunu** birbaşa doğrulayan test yox idi. QA əlavə etdi: `Increasing_The_Requested_Count_Strictly_Grows_The_Document` (theory, 1→2, 2→3, 9→10) — hər addımda sənəd ciddi şəkildə böyüyür (hər əlavə surət öz ad/qiymət/rəqəm mətnini əlavə edir, təkcə kod şəkli paylaşılsa da); `Item_Order_In_The_Request_Is_Reflected_In_The_Document` — eyni iki fərqli malın sırası dəyişdirildikdə (A,B ↔ B,A) sənəd byte-səviyyəsində fərqlənir, yəni sıra silinmir/qruplaşdırılmır/sortlanmır, sorğuda gəldiyi ardıcıllıqla düzülür. |

## Test case nəticələri (issue-dakı ssenarilər)

| # | Ssenari | Nəticə | Faktiki davranış / Qeyd |
|---|---|---|---|
| TC-1 | Barkodsuz mala `generate-barcode` → 200, `SDK\d{7}` | ✅ PASS | `ProductsApiTests.Generate_Barcode_Assigns_Unique_SDK_Code_To_Barcodeless_Product` |
| TC-2 | Barkodlu mala `generate-barcode` → 409 | ✅ PASS | `ProductsApiTests.Generate_Barcode_Returns_409_When_Product_Already_Has_One` |
| TC-3 | Seller `generate-barcode` çağırsa → 403 | ✅ PASS | `ProductsApiTests.Seller_Cannot_Generate_Barcode_Returns_403` |
| TC-4 | `labels.pdf` autentifikasiyasız → 401 | ✅ PASS | `ExportsApiTests.Labels_Pdf_Requires_Authentication` |
| TC-5 | Barkodlu mal(lar) üçün `labels.pdf` → 200, `%PDF`, `attachment; filename="etiketler-...pdf"` | ✅ PASS | `ExportsApiTests.Labels_Pdf_Returns_Pdf_With_Magic_Bytes_For_Barcoded_Products` + unit `Produces_A_Pdf_Named_After_The_Business_Date` (tam tarix formatı) |
| TC-6 | Barkodsuz mal(lar) `labels.pdf`-ə daxil olsa → 400, adlarla | ✅ PASS | `Labels_Pdf_Returns_400_When_A_Product_Has_No_Barcode`, unit `Names_Every_Barcodeless_Product_Once` |
| TC-7 | Cəmi say > 500 → 400 | ✅ PASS | `Labels_Pdf_Returns_400_When_Total_Count_Exceeds_500`, unit `Accepts_Exactly_500_Labels` (sərhəd) |
| TC-8 | `count <= 0` → 400 | ✅ PASS | `Labels_Pdf_Returns_400_When_A_Count_Is_Not_Positive` |
| TC-9 | Boş/`null` body → paylaşılan `{code,message}` 400 forması | ✅ PASS | `Labels_Pdf_Returns_400_With_The_Shared_Error_Shape_For_An_Empty_Body` |
| TC-10 | `type: "qr"` → QR render olunur, dekod olunanda barkod dəyərini verir | ✅ PASS | `LabelCodeImageRendererTests.Qr_Image_Decodes_Back_To_The_Value_As_Qr` + QA-nın yeni `Qr_And_Barcode_Requests_Produce_Different_Documents_For_The_Same_Input` |
| TC-11 | Eyni mal təkrar `productId` ilə göndərilsə → bir dəfə lookup, hər giriş öz `count`-u qədər əlavə edir | ✅ PASS | `Accepts_A_Repeated_Product_And_Looks_It_Up_Once` |
| TC-12 | Uçdan-uça: barkodsuz mal → `generate-barcode` → dərhal çap oluna bilir | ✅ PASS | `Generated_Barcode_Makes_A_Product_Printable` |
| TC-13 (QA) | Grid 3×8, 63×34mm, `MaxLabels=500` sabitləri koda uyğundur | ✅ PASS | QA: `LabelSheetLayoutSpecTests.Grid_Is_Three_Columns_By_Eight_Rows_Of_63x34mm_Labels_With_A_Cut_Gap`, `Sheet_Wide_Label_Cap_Is_500` |
| TC-14 (QA) | Qiymət "12.50 ₼" formatı, mədəni parametrlərdən asılı olmayaraq | ✅ PASS | QA: `Price_Renders_As_Two_Decimals_Followed_By_The_Manat_Sign` (4 hal), `Price_Format_Does_Not_Follow_The_Servers_Current_Culture` |
| TC-15 (QA) | Uzun mal adı kəsilir (`...`), qısa ad dəyişmir, sərhəd (40 simvol) dəyişmir | ✅ PASS | QA: `Long_Product_Name_Is_Truncated_With_An_Ellipsis`, `Short_Product_Name_Is_Printed_Unchanged`, `Name_At_Exactly_The_Cap_Is_Not_Truncated` |
| TC-16 (QA) | `count` artdıqca sənəd ciddi böyüyür (silinmir/kəsilmir) | ✅ PASS | QA: `Increasing_The_Requested_Count_Strictly_Grows_The_Document` (1→2, 2→3, 9→10) |
| TC-17 (QA) | Sorğudakı sıra sənəddə qorunur (qruplaşma/sortlama yoxdur) | ✅ PASS | QA: `Item_Order_In_The_Request_Is_Reflected_In_The_Document` |
| TC-18 (QA) | `type` bayrağı faktiki fərqli sənəd yaradır (sükutla nəzərə alınmır) | ✅ PASS | QA: `Qr_And_Barcode_Requests_Produce_Different_Documents_For_The_Same_Input` |

## Tapılan buglar

Heç bir bug tapılmadı. PR-in özündəki test örtüyü artıq yüksək səviyyədə idi (hər `400` səthi, 409, 403, 401, sərhəd dəyərləri, overflow qorunması, kod şəklinin paylaşılması, real ZXing decode ilə barkod/QR doğruluğu real HTTP + real SQL Server üzərində inteqrasiya testləri ilə örtülmüşdü). Tapılan yeganə şey **zəiflik** idi, **bug** deyil: AC6/AC7/AC8-in bəzi hissələri (grid ölçüləri, qiymət formatı, ad kəsilməsi, `type` bayrağının faktiki renderer seçiminə təsiri, `count`-un sənədə əks olunması, sorğu sırasının qorunması) yalnız kod baxışı ilə "sənədləşdirilirdi", müstəqil test yox idi. Bu, tapşırıqda tələb olunduğu kimi 15 yeni QA testi ilə bağlandı (aşağıya bax) — kodun özündə heç bir dəyişiklik edilmədi.

## Əlavə edilən QA testləri

- `tests/MayaPro.WarehouseApi.Modules.Exports.Tests/LabelSheetLayoutSpecTests.cs` (yeni fayl, 10 test) — reflection ilə `ExportProductLabelsPdfHandler`-in private grid sabitlərini (`Columns`, `Rows`, `LabelWidthMm`, `LabelHeightMm`, `GapMm`, `MaxLabels`) və private `FormatPrice`/`TruncateName` metodlarını birbaşa sınayır. Reflection seçildi, çünki bu detallar bilərəkdən private saxlanılıb (ictimai seam yoxdur) və QA-nın tətbiq kodunu dəyişmək icazəsi yoxdur.
- `tests/MayaPro.WarehouseApi.Modules.Exports.Tests/ExportProductLabelsPdfHandlerTests.cs` (mövcud fayla 5 yeni test əlavə olundu) — `Qr_And_Barcode_Requests_Produce_Different_Documents_For_The_Same_Input`, `Increasing_The_Requested_Count_Strictly_Grows_The_Document` (theory, 3 hal), `Item_Order_In_The_Request_Is_Reflected_In_The_Document`.
- Commit: `60fa45c` — `test: qa BE-12 ek test ortuyu`, branch `task/BE-12-barcode-label-pdf`-ə push olunub.

## İcra olunan test əmrləri

```bash
git -C ".../backend" fetch origin
git -C ".../backend" status
# On branch task/BE-12-barcode-label-pdf, up to date with origin, clean

git -C ".../backend" log main..task/BE-12-barcode-label-pdf --oneline
# fa7306b refactor: senior backend review duzelisleri
# 1668b2e feat: barkod generasiyasi ve etiket pdf

dotnet build -p:BaseOutputPath=bin-qa2/
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test -p:BaseOutputPath=bin-qa2/          # QA testləri əlavə olunmazdan əvvəl
# TOTAL: 309/309 passed, 0 failed, 0 skipped

# ... LabelSheetLayoutSpecTests.cs yaradıldı, ExportProductLabelsPdfHandlerTests.cs-ə 5 test əlavə olundu ...

dotnet test tests/MayaPro.WarehouseApi.Modules.Exports.Tests -p:BaseOutputPath=bin-qa2/ -v n
# MayaPro.WarehouseApi.Modules.Exports.Tests   35/35 passed (20 mövcud + 15 yeni QA testi)

dotnet build -p:BaseOutputPath=bin-qa2/
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test -p:BaseOutputPath=bin-qa2/          # QA testləri əlavə olunduqdan sonra, bütün solution
# MayaPro.WarehouseApi.SharedKernel.Tests            6/6 passed
# MayaPro.WarehouseApi.Modules.DayEnd.Tests          4/4 passed
# MayaPro.WarehouseApi.Modules.Customers.Tests       6/6 passed
# MayaPro.WarehouseApi.Modules.Products.Tests        31/31 passed
# MayaPro.WarehouseApi.Modules.Suppliers.Tests       12/12 passed
# MayaPro.WarehouseApi.Modules.Exports.Tests         35/35 passed (15 yeni BE#12 QA testi daxil)
# MayaPro.WarehouseApi.Modules.Sales.Tests           20/20 passed
# MayaPro.WarehouseApi.Modules.Reports.Tests         17/17 passed
# MayaPro.WarehouseApi.Modules.Expenses.Tests        52/52 passed
# MayaPro.WarehouseApi.Modules.Auth.Tests            4/4 passed
# MayaPro.WarehouseApi.IntegrationTests              137/137 passed (real SQL Server üzərində)
# TOTAL: 324/324 passed, 0 failed, 0 skipped

git -C ".../backend" add tests/.../ExportProductLabelsPdfHandlerTests.cs tests/.../LabelSheetLayoutSpecTests.cs
git -C ".../backend" commit -m "test: qa BE-12 ek test ortuyu"
git -C ".../backend" push origin task/BE-12-barcode-label-pdf
# fa7306b..60fa45c  task/BE-12-barcode-label-pdf -> task/BE-12-barcode-label-pdf
```

## Tövsiyələr

- Reqressiya riski aşkarlanmadı; branch `task/BE-12-barcode-label-pdf` QA-nı problemsiz keçdi.
- Bug tapılmadı — backend taskı **Done** statusuna keçirilə bilər.
- Gələcək üçün qeyd (bloklayıcı deyil): AC6/7/8-in vizual/struktur tərəflərini (real PDF-də neçə fiziki səhifə yarandığı, grid xanalarının koordinatları) daha da sərt sınamaq üçün test layihəsinə PDF-parsing kitabxanası (məs. PdfPig) əlavə etmək düşünülə bilər — bu sessiyada şəbəkə/asılılıq əlavəsi riskindən qaçmaq üçün yalnız mövcud alətlərlə (reflection, ZXing decode, byte-səviyyəli müqayisə) işləndi.
