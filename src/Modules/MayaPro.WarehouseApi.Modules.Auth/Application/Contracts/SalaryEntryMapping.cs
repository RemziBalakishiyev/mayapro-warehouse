using MayaPro.WarehouseApi.Modules.Auth.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;

/// <summary>Maps the <see cref="SalaryEntry"/> entity to its wire DTO.</summary>
public static class SalaryEntryMapping
{
    public static SalaryEntryDto ToDto(this SalaryEntry entry) =>
        new(
            entry.Id,
            entry.UserId,
            entry.Type.ToCode(),
            entry.Amount,
            entry.Note,
            entry.Date,
            entry.Month,
            entry.CreatedByUserId,
            entry.CreatedAt);
}
