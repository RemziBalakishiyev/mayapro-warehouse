# Documentation Workflow

Bu repo-da davamlı project knowledge sistemi var: `docs/INDEX.md` router-dır, sənədlər mövzu üzrə bölünüb.

## Sessiya başlanğıcı (oxuma qaydası)

1. Əvvəlcə YALNIZ `docs/INDEX.md` oxu (qısa router-dır).
2. INDEX-dən cari task-a aid olan sənədləri seç və yalnız onları aç.
3. Bütün docs qovluğunu heç vaxt kor-koranə oxuma.
4. Source code həmişə **ultimate source of truth**-dur. Sənədlə kod ziddiyyət təşkil edirsə: kodu yoxla, sənədi düzəlt (əksinə yox).

## Hər tamamlanmış implementation task-dan sonra (yazma qaydası)

`git diff` / dəyişən faylları analiz et və uyğun sənədləri yenilə:

| Dəyişiklik növü | Yenilənəcək sənəd |
|---|---|
| Business davranışı (qayda, hesablama, zəncir) | `docs/business/BUSINESS-RULES.md` |
| User/system flow (addımlar, ardıcıllıq) | `docs/flows/<FLOW>.md` (uyğun olan) |
| Endpoint, request/response, validation, authorization, error kodu | `docs/api/API-OVERVIEW.md` və/və ya `docs/api/ERROR-CONTRACT.md` |
| Entity, migration, relation, index, query davranışı | `docs/database/DATABASE.md`, `docs/database/ENTITY-RELATIONS.md` |
| Modul asılılığı, yeni kontrakt, arxitektura qərarı | `docs/architecture/MODULES.md`, `docs/architecture/ARCHITECTURE.md`; əhəmiyyətli qərar üçün `docs/decisions/`-də yeni qeyd |
| Yeni termin / anlayış | `docs/business/GLOSSARY.md` |
| Hər əhəmiyyətli dəyişiklik | `docs/changes/CHANGELOG.md`-yə 1-3 sətirlik qeyd (tarix + commit prefiksi ilə) |

Yenilədiyin hər sənəddə `Last Updated` tarixini təzələ.

## Sənəd qaydaları

- Yeni sənəd yaratmazdan əvvəl `docs/INDEX.md`-də uyğun mövcud sənəd axtar — duplicate yaratma.
- Yeni sənəd yaratdınsa, `docs/INDEX.md`-ə sətir əlavə et.
- Yalnız faktiki koddan təsdiqlənmiş məlumat yaz; ehtimalı fakt kimi yazma.
- Köhnəlmiş məlumatı sil və ya düzəlt — "tarixi qeyd" kimi saxlama.
- Implementation detail yox, uzunmüddətli faydalı bilik saxla (niyə/nə, necə yox).
- Böyük code block kopyalama; class/metod siyahılarını təkrarlama — file path referansı kifayətdir.
- Hər sənəddə sonda iki bölmə: `## Last Updated` (YYYY-MM-DD + qısa səbəb) və `## Related Code` (əlaqəli qovluq/fayl yolları).
- Sənədlər Azərbaycanca, texniki terminlər olduğu kimi (ingiliscə) qala bilər.

## Task sonu hesabat formatı

Hər task-ın yekun cavabında göstər:

- **Code files changed:** dəyişən kod faylları
- **Documentation files updated:** yenilənən sənədlər
- **Documentation impact:** sənəd yenilənməyibsə, bir cümlə ilə səbəbi (məs. "yalnız refactor, davranış dəyişməyib")
