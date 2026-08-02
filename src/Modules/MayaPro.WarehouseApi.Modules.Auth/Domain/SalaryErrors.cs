using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// Business errors for the salary side of the Auth module (BE#28). Messages are user-facing (Azerbaijani);
/// the code suffix drives the HTTP status (<c>...NotFound</c> → 404, everything else → 400).
/// </summary>
public static class SalaryErrors
{
    public static readonly Error EntryNotFound =
        new("Salary.EntryNotFound", "Maaş əməliyyatı tapılmadı");

    public static readonly Error InvalidType =
        new("Salary.InvalidType", "Maaş əməliyyatının növü yanlışdır");

    public static readonly Error InvalidMonth =
        new("Salary.InvalidMonth", "Ay formatı yanlışdır (yyyy-MM)");
}
