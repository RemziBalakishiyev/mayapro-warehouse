namespace MayaPro.WarehouseApi.Modules.Activity.Application.Contracts;

/// <summary>
/// An activity entry as returned by the API. Field names follow the frontend <c>Activity</c> type:
/// <c>employeeId</c> (the acting user), <c>action</c> (the label) and <c>detail</c>, plus a name snapshot.
/// </summary>
public sealed record ActivityDto(
    Guid Id,
    Guid? EmployeeId,
    string Action,
    string Detail,
    string? UserName,
    DateTime Date);
