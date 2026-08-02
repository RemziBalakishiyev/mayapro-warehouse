using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetTodayClosing;

/// <summary>Returns today's closing if the day has been closed, otherwise null.</summary>
public sealed class GetTodayClosingHandler(IDayEndDbContext db, IDateProvider dateProvider)
{
    public async Task<ClosingDto?> Handle(CancellationToken ct)
    {
        // "Today" must be the business-zone day (ADR-0005), the same day CloseDayHandler closes — a raw
        // DateTime.UtcNow.Date would disagree with it for part of every day (e.g. Asia/Baku is UTC+4), so a
        // day already closed could read as "not closed" here right after UTC midnight.
        DateOnly today = dateProvider.Today;

        Closing? closing = await db.Closings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Date == today, ct);

        return closing?.ToDto();
    }
}
