using MayaPro.WarehouseApi.Modules.Expenses.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Expenses.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.GetExpenseTypes;

/// <summary>Returns every managed expense type, ordered by name.</summary>
public sealed class GetExpenseTypesHandler(IExpensesDbContext db)
{
    public async Task<IReadOnlyList<ExpenseTypeDto>> Handle(CancellationToken ct)
    {
        return await db.ExpenseTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new ExpenseTypeDto(t.Id, t.Name))
            .ToListAsync(ct);
    }
}
