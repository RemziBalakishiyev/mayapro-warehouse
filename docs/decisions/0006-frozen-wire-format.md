# ADR-0006: Dondurulmuş wire format — Azərbaycanca kontrakt dəyərləri

**Status:** Qəbul edilib

## Qərar

Frontend ilə mübadilə olunan sabit string dəyərlər (ödəniş növləri `Nağd/Kart/Nisyə`, xərc kateqoriyaları, rol kodları `sahib/menecer/satici`) API kontraktının bir hissəsidir və **heç vaxt dəyişdirilə bilməz**. Hamısı tək yerdə yaşayır: `WireFormat.cs`. C# identifikatorları ingiliscədir (refactor `482d9fc`), amma wire dəyərləri Azərbaycanca qaldı.

JSON konvensiyaları: camelCase, tarixlər ISO 8601, pul `decimal` (JSON number). DTO referansı: `docs/index.ts` (frontend tipləri), davranış referansı: `docs/handlers.ts` (frontend mock-ları).

## Last Updated
2026-07-25

## Related Code
- `src/MayaPro.WarehouseApi.SharedKernel/Contracts/WireFormat.cs`
- `docs/index.ts`, `docs/handlers.ts`
