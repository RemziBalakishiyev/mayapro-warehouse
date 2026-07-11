using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSuppliers;

/// <summary>Returns every supplier, newest first.</summary>
public sealed class GetSuppliersHandler(ISuppliersDbContext db)
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(CancellationToken ct)
    {
        List<Supplier> suppliers = await db.Suppliers
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return suppliers.Select(s => s.ToDto()).ToList();
    }
}
