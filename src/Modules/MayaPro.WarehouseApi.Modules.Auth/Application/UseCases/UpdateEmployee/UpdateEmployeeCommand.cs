namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.UpdateEmployee;

/// <summary>
/// Edits a payroll record (BE#57). <c>Id</c> comes from the route; <c>IsActive</c> is not editable here —
/// activating and deactivating are their own explicit routes.
/// <para>
/// <b>There is deliberately no <c>MonthlySalary</c> here (BE#59).</b> The agreed salary is changed only through
/// <c>PUT /api/employees/{id}/salary</c>, which is <c>OwnerOnly</c>, while this route is <c>OwnerOrManager</c>.
/// Carrying the figure in the edit body broke that rule twice over: a manager could set any salary through the
/// wider route, and — worse — an edit form that sensibly sends only <c>{fullName, position}</c> bound the
/// missing field to the record default <c>0m</c> and silently wiped the agreed salary, dragging every past
/// month's <c>remaining</c> below zero. A field the caller may not send safely does not belong in the contract:
/// leaving it out means the salary simply cannot be reached from here, by anyone, by accident or otherwise.
/// A <c>monthlySalary</c> that still arrives in the JSON is ignored, exactly like any other unknown field.
/// </para>
/// </summary>
public sealed record UpdateEmployeeCommand(
    Guid Id,
    string FullName,
    string? Phone,
    string Position,
    string? Note = null);
