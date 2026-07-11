using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Activity.Domain;

/// <summary>
/// A recorded user action. <see cref="Type"/> is the action label and <see cref="Message"/> the detail —
/// these map to the frontend <c>Activity.action</c> / <c>Activity.detail</c>. <see cref="UserName"/> is a
/// snapshot so the feed reads well even if the user is later renamed.
/// </summary>
public sealed class ActivityLog : Entity
{
    // EF Core constructor.
    private ActivityLog() { }

    private ActivityLog(string type, string message, Guid? userId, string? userName)
    {
        Type = type;
        Message = message;
        UserId = userId;
        UserName = userName;
    }

    public string Type { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public static ActivityLog Create(string type, string message, Guid? userId, string? userName) =>
        new(type, message, userId, userName);
}
