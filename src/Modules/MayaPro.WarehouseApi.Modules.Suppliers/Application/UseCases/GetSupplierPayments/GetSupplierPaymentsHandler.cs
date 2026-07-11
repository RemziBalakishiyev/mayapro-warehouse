using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierPayments;

/// <summary>Returns a supplier's payments, newest first.</summary>
public sealed class GetSupplierPaymentsHandler(ISuppliersDbContext db)
{
    public async Task<IReadOnlyList<SupplierPaymentDto>> Handle(Guid supplierId, CancellationToken ct)
    {
        List<SupplierPayment> payments = await db.SupplierPayments
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.Date)
            .ToListAsync(ct);

        return payments.Select(p => p.ToDto()).ToList();
    }
}
