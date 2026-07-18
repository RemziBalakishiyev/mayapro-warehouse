using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application;

/// <summary>
/// The Suppliers module's implementation of <see cref="ISuppliersModule"/>: debt totals and name lookups.
/// </summary>
internal sealed class SuppliersModuleContract(ISuppliersDbContext db) : ISuppliersModule
{
    public async Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        await db.Suppliers.AsNoTracking().SumAsync(s => s.Debt, cancellationToken);

    public async Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        List<Guid> idSet = ids.Distinct().ToList();
        if (idSet.Count == 0)
            return new Dictionary<Guid, string>();

        return await db.Suppliers
            .AsNoTracking()
            .Where(s => idSet.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    }
}
