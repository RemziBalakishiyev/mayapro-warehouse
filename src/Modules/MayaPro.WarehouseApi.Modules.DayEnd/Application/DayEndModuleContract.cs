using MayaPro.WarehouseApi.Modules.DayEnd.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.DayEnd.Application;

/// <summary>The DayEnd module's implementation of <see cref="IDayEndModule"/>: the most recent closing.</summary>
internal sealed class DayEndModuleContract(IDayEndDbContext db) : IDayEndModule
{
    public async Task<ClosingSnapshot?> GetLastClosingAsync(CancellationToken cancellationToken = default)
    {
        var closing = await db.Closings
            .AsNoTracking()
            .OrderByDescending(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken);

        return closing is null ? null : new ClosingSnapshot(closing.Date, closing.ActualCash);
    }

    public async Task<bool> ClosingExistsAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        await db.Closings.AsNoTracking().AnyAsync(c => c.Date == date, cancellationToken);
}
