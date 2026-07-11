using MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Activity.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Activity.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Activity.Application.UseCases.GetActivity;

/// <summary>Returns a page of activity entries, newest first.</summary>
public sealed class GetActivityHandler(IActivityDbContext db)
{
    public async Task<IReadOnlyList<ActivityDto>> Handle(int take, int skip, CancellationToken ct)
    {
        int safeTake = take is > 0 and <= 200 ? take : 50;
        int safeSkip = skip > 0 ? skip : 0;

        List<ActivityLog> logs = await db.ActivityLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Skip(safeSkip)
            .Take(safeTake)
            .ToListAsync(ct);

        return logs.Select(a => a.ToDto()).ToList();
    }
}
