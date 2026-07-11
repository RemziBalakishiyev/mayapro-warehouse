using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetTodayClosing;

/// <summary>Returns today's closing if the day has been closed, otherwise null.</summary>
public sealed class GetTodayClosingHandler(IDayEndDbContext db)
{
    public async Task<ClosingDto?> Handle(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Closing? closing = await db.Closings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Date == today, ct);

        return closing?.ToDto();
    }
}
