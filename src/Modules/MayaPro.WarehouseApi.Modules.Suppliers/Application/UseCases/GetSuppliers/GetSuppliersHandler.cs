using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSuppliers;

/// <summary>
/// Returns every supplier, newest first, enriched with paid-amount and last-payment. The payment stats
/// come from a single grouped query — never a query per supplier.
/// </summary>
public sealed class GetSuppliersHandler(ISuppliersDbContext db)
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(CancellationToken ct)
    {
        List<Supplier> suppliers = await db.Suppliers
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        Dictionary<Guid, (decimal Paid, DateTime Last)> paymentStats = await db.SupplierPayments
            .AsNoTracking()
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Paid = g.Sum(p => p.Amount), Last = g.Max(p => p.Date) })
            .ToDictionaryAsync(x => x.SupplierId, x => (x.Paid, x.Last), ct);

        return suppliers
            .Select(s =>
            {
                (decimal paid, DateTime? lastPayment) = paymentStats.TryGetValue(s.Id, out var stat)
                    ? (stat.Paid, stat.Last)
                    : (0m, (DateTime?)null);
                return s.ToDto(paid, lastPayment);
            })
            .ToList();
    }
}
