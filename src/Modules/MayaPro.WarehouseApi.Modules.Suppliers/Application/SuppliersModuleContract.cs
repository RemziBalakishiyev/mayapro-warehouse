using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application;

/// <summary>The Suppliers module's implementation of <see cref="ISuppliersModule"/>: our total debt to suppliers.</summary>
internal sealed class SuppliersModuleContract(ISuppliersDbContext db) : ISuppliersModule
{
    public async Task<decimal> GetTotalDebtAsync(CancellationToken cancellationToken = default) =>
        await db.Suppliers.AsNoTracking().SumAsync(s => s.Debt, cancellationToken);
}
