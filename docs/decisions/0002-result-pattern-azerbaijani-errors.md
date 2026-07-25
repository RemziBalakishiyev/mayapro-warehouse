# ADR-0002: Result pattern + Azərbaycanca xəta mesajları

**Status:** Qəbul edilib

## Qərar

Biznes qaydası pozulanda exception atılmır — `Result.Failure(new Error(code, message))` qayıdır. Error mesajları istifadəçiyə birbaşa göstərildiyi üçün **həmişə Azərbaycanca**; log mesajları ingiliscə.

HTTP status error code-un suffiksindən avtomatik seçilir (`NotFound`→404, `Conflict`/`AlreadyExists`/`AlreadyClosed`→409, qalanı→400) — modullar HTTP-agnostikdir. Detallar: `docs/api/ERROR-CONTRACT.md`.

## Səbəb

Frontend api-client `{code, message}` formatını tanıyıb toast göstərir; exception-lar yalnız həqiqi qəza halları üçündür (GlobalExceptionHandler → 500).

## Last Updated
2026-07-25

## Related Code
- `src/MayaPro.WarehouseApi.SharedKernel/Application/` (Result, Error, ResultExtensions)
