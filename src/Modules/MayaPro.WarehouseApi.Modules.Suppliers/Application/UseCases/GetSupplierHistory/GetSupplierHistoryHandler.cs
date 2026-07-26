using MayaPro.WarehouseApi.Modules.Suppliers.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Suppliers.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Suppliers.Application.UseCases.GetSupplierHistory;

/// <summary>
/// Returns a supplier's complete debt history in chronological order (oldest first): the opening balance
/// and every payment. Each source is one query; they are merged and sorted by timestamp in memory. Unlike
/// <see cref="GetSupplierPayments.GetSupplierPaymentsHandler"/> (kept for backward compatibility), this
/// endpoint also surfaces the opening-balance row.
/// </summary>
public sealed class GetSupplierHistoryHandler(ISuppliersDbContext db)
{
    public async Task<IReadOnlyList<SupplierHistoryEntryDto>> Handle(Guid supplierId, CancellationToken ct)
    {
        List<SupplierDebtAdjustment> adjustments = await db.SupplierDebtAdjustments
            .AsNoTracking()
            .Where(a => a.SupplierId == supplierId)
            .ToListAsync(ct);

        List<SupplierPayment> payments = await db.SupplierPayments
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .ToListAsync(ct);

        var entries = new List<SupplierHistoryEntryDto>(adjustments.Count + payments.Count);

        entries.AddRange(adjustments.Select(a => new SupplierHistoryEntryDto(
            a.Date, SupplierHistoryEntryType.InitialDebt, a.Amount, a.Note)));

        entries.AddRange(payments.Select(p => new SupplierHistoryEntryDto(
            p.Date, SupplierHistoryEntryType.Payment, p.Amount, p.Note)));

        return entries.OrderBy(e => e.Date).ToList();
    }
}
