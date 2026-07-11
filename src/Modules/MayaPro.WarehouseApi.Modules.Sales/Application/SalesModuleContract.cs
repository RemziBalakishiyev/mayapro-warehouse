using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Sales.Application;

/// <summary>The Sales module's implementation of <see cref="ISalesModule"/>: day totals for day-end.</summary>
internal sealed class SalesModuleContract(ISalesDbContext db) : ISalesModule
{
    public async Task<SalesDayTotals> GetDayTotalsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        DateTime start = date.ToDateTime(TimeOnly.MinValue);
        DateTime end = start.AddDays(1);

        var byType = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= start && s.Date < end)
            .GroupBy(s => s.PaymentType)
            .Select(g => new { Type = g.Key, Total = g.Sum(s => s.TotalAmount) })
            .ToListAsync(cancellationToken);

        decimal Total(PaymentType type) => byType.FirstOrDefault(x => x.Type == type)?.Total ?? 0m;

        return new SalesDayTotals(Total(PaymentType.Nagd), Total(PaymentType.Kart), Total(PaymentType.Nisye));
    }
}
