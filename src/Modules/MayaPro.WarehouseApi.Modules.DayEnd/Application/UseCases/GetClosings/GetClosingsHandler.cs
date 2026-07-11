using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.DayEnd.Application.Contracts;
using MayaPro.WarehouseApi.Modules.DayEnd.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.GetClosings;

/// <summary>Returns all closings, newest day first.</summary>
public sealed class GetClosingsHandler(IDayEndDbContext db)
{
    public async Task<IReadOnlyList<ClosingDto>> Handle(CancellationToken ct)
    {
        List<Closing> closings = await db.Closings
            .AsNoTracking()
            .OrderByDescending(c => c.Date)
            .ToListAsync(ct);

        return closings.Select(c => c.ToDto()).ToList();
    }
}
