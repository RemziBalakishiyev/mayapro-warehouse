using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Auth.Domain;

/// <summary>
/// Business errors for the payroll side of the Auth module (BE#57). Messages are user-facing (Azerbaijani);
/// the code suffix drives the HTTP status (<c>...NotFound</c> → 404).
/// <para>
/// Deliberately separate from <see cref="AuthErrors"/>: "İstifadəçi tapılmadı" is about a login account, and
/// after BE#57 the salary routes no longer look at accounts at all. Reusing <c>Auth.UserNotFound</c> there
/// would tell the operator to go looking in the wrong list.
/// </para>
/// </summary>
public static class EmployeeErrors
{
    public static readonly Error NotFound =
        new("Employee.NotFound", "İşçi tapılmadı");
}
