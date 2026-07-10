# MayaPro.WarehouseApi

## Mənbələr
- Arxitektura: docs/backend-arxitektura.md — struktura DƏQİQ əməl et (adlar MayaPro.WarehouseApi.* ilə)
- DTO referansı: docs/types-index.ts (frontend tipləri = API kontraktı)
- Biznes məntiqi referansı: docs/handlers.ts (frontend mock handler-ləri)

## Stack
.NET 8, ASP.NET Core Minimal API, EF Core 8, SQL Server, JWT, FluentValidation, Serilog

## Qaydalar
- Modular monolith: modullar bir-birinin cədvəlinə toxunmur, əlaqə yalnız SharedKernel.Contracts ilə
- Biznes xətaları exception yox, Result pattern ilə; istifadəçi mesajları Azərbaycanca
- Bütün pul sahələri decimal(18,2)
- Hər mərhələdən sonra: dotnet build xətasız + testlər yaşıl, sonra commit