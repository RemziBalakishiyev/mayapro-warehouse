# MayaPro.WarehouseApi

Bazar (Sədərək) anbar-satış sistemi backend-i. Modular monolith, 11 modul.

## Stack

.NET 8, ASP.NET Core Minimal API, EF Core 8, SQL Server, JWT, FluentValidation, Serilog, QuestPDF, ClosedXML

## Knowledge sistemi (ƏVVƏL BUNU OXU)

1. Hər sessiyada əvvəlcə **yalnız** `docs/INDEX.md` oxu — qısa router-dır.
2. INDEX-dən cari task-a aid sənədləri seç, yalnız onları aç. Bütün `docs/`-u kor-koranə yükləmə.
3. Source code həmişə **ultimate source of truth**. Sənəd kodla ziddiyyətdədirsə: kodu yoxla, sənədi düzəlt.
4. Tam workflow qaydaları: `.claude/rules/documentation.md`.

Referans faylları (kontrakt): `docs/index.ts` (frontend tipləri = DTO spesifikasiyası), `docs/handlers.ts` (biznes davranış referansı), `docs/seed.ts` (seed referansı). `docs/backend-arxitektura.md` tarixi dizayn planıdır — faktiki vəziyyət `docs/architecture/`-dadır.

## Dəyişməz qaydalar

- Modullar bir-birinin cədvəlinə toxunmur; əlaqə yalnız `SharedKernel.Contracts` interfeysləri ilə
- Biznes xətaları exception yox, Result pattern; istifadəçi mesajları Azərbaycanca; error code suffiksi HTTP statusu təyin edir (`NotFound`→404, `Conflict/AlreadyExists/AlreadyClosed`→409)
- Bütün pul sahələri decimal(18,2)
- Wire dəyərləri (Nağd/Kart/Nisyə, rol kodları...) dondurulub — `WireFormat.cs`-dən istifadə et, heç vaxt dəyişmə
- "Bu gün" məntiqində həmişə `IDateProvider` (Asia/Baku); `DateTime.Now/Today` qadağandır
- Modullararası zəncirlər `IUnitOfWork` transaction-ında; kontrakt metodları save etmir — caller commit edir
- Hər mərhələdən sonra: `dotnet build` xətasız + testlər yaşıl, sonra commit

## Implementation-dan sonra (documentation workflow)

1. `git diff`-ə bax, documentation impact-i müəyyən et.
2. Yalnız təsirlənən sənədləri yenilə:
   - Biznes davranışı → `docs/business/BUSINESS-RULES.md`
   - Flow dəyişikliyi → `docs/flows/<FLOW>.md`
   - Endpoint/validation/authorization/error → `docs/api/`
   - Entity/migration/index → `docs/database/`
   - Modul asılılığı/arxitektura qərarı → `docs/architecture/`, əhəmiyyətli qərar → `docs/decisions/`
   - Əhəmiyyətli dəyişiklik → `docs/changes/CHANGELOG.md`-yə qısa qeyd
3. `docs/INDEX.md`-i yalnız sənəd yaradılanda/silinəndə/köçürüləndə yenilə.
4. Yeni sənədi yalnız uyğun mövcud sənəd olmayanda yarat; duplicate yaratma; yalnız kodda təsdiqlənən faktı yaz.
5. Formatting, şərh, test-data və davranışı dəyişməyən refactor üçün sənəd yeniləməsi lazım deyil.

## Task sonu hesabatı

Yekun cavabda həmişə göstər:

- **Code files changed**
- **Documentation files updated**
- **Tests executed**
- **Documentation impact** (yoxdursa — səbəbi bir cümlə)
